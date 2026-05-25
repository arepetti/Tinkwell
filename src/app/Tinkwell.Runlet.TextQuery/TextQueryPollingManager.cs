using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Runlet.TextQuery.Configuration;
using Tinkwell.Runner;
using Tinkwell.Runlet.TextQuery.Transports;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Runlet.TextQuery;

internal sealed class TextQueryPollingManager : BackgroundService
{
    private readonly TextQueryRunletOptions _options;
    private readonly IServiceDiscovery _discovery;
    private readonly ILogger<TextQueryPollingManager> _logger;
    private readonly ConcurrentBag<ITextTransport> _transports = [];

    public TextQueryPollingManager(
        TextQueryRunletOptions options,
        IServiceDiscovery discovery,
        ILogger<TextQueryPollingManager> logger)
    {
        _options = options;
        _discovery = discovery;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TextQueryConfig config;
        try
        {
            var parser = new TextQueryConfigParser(_logger);
            var configPath = _options.ConfigPath
                ?? Environment.GetEnvironmentVariable("TINKWELL_CONFIG");
            if (configPath is null)
            {
                _logger.LogWarning("No TextQuery configuration path specified");
                return;
            }

            config = await parser.LoadFileAsync(configPath, cancellationToken: stoppingToken);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse TextQuery configuration");
            return;
        }

        if (config.Sources.Count == 0)
        {
            _logger.LogInformation("No query blocks found in configuration");
            return;
        }

        MeasuresGrpc.Measures.MeasuresClient? measuresClient = null;
        for (int attempt = 0; attempt < 30 && !stoppingToken.IsCancellationRequested; ++attempt)
        {
            try
            {
                var svc = await _discovery.DiscoverAsync("measures", stoppingToken);
                if (svc is not null)
                {
                    measuresClient = await _discovery
                        .CreateInstanceAsync<MeasuresGrpc.Measures.MeasuresClient>(svc, stoppingToken);
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Measures service discovery attempt {Attempt} failed", attempt + 1);
            }

            await Task.Delay(1000, stoppingToken);
        }

        if (measuresClient is null)
        {
            _logger.LogError("Could not discover measures service — TextQuery polling disabled");
            return;
        }

        var tasks = new List<Task>();
        foreach (var source in config.Sources)
        {
            tasks.Add(PollSourceAsync(source, measuresClient, stoppingToken));
        }

        await Task.WhenAll(tasks);
    }

    private async Task PollSourceAsync(
        TextQuerySourceDefinition source,
        MeasuresGrpc.Measures.MeasuresClient measuresClient,
        CancellationToken ct)
    {
        ITextTransport transport;
        try
        {
            transport = CreateTransport(source);
            _transports.Add(transport);
            await transport.ConnectAsync(ct);
            _logger.LogInformation("TextQuery connected to {Transport} source '{Name}'",
                source.Transport, source.Name);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect TextQuery source '{Name}'", source.Name);
            return;
        }

        var regexCache = new Dictionary<string, Regex>();
        foreach (var read in source.Reads)
            regexCache[read.Name] = new Regex(read.Pattern, RegexOptions.Compiled);

        while (!ct.IsCancellationRequested)
        {
            foreach (var read in source.Reads)
            {
                try
                {
                    var response = await transport.QueryAsync(
                        read.SendCommand, source.LineTerminator, source.ReadTimeoutMs, ct);

                    if (string.IsNullOrEmpty(response))
                        continue;

                    var regex = regexCache[read.Name];
                    var match = regex.Match(response);
                    if (!match.Success || match.Groups.Count <= read.CaptureGroup)
                    {
                        _logger.LogDebug(
                            "TextQuery '{Source}/{Read}': pattern did not match response '{Response}'",
                            source.Name, read.Name, response.Length > 200 ? response[..200] : response);
                        continue;
                    }

                    var captured = match.Groups[read.CaptureGroup].Value;
                    if (!double.TryParse(captured, CultureInfo.InvariantCulture, out var value))
                    {
                        _logger.LogDebug(
                            "TextQuery '{Source}/{Read}': captured '{Captured}' is not numeric",
                            source.Name, read.Name, captured);
                        continue;
                    }

                    value *= read.Scale;

                    await measuresClient.UpdateAsync(new MeasuresGrpc.UpdateMeasureRequest
                    {
                        Name = read.MeasureName,
                        Value = new MeasuresGrpc.MeasureValueProto
                        {
                            Type = "number",
                            NumericValue = value,
                        },
                    }, cancellationToken: ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error in TextQuery '{Source}/{Read}'",
                        source.Name, read.Name);
                }
            }

            try
            {
                await Task.Delay(source.PollInterval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    internal static ITextTransport CreateTransport(TextQuerySourceDefinition source) =>
        source.Transport switch
        {
            TextQueryTransport.Tcp => new TcpTextTransport(
                source.Host ?? throw new InvalidOperationException(
                    $"TCP query source '{source.Name}' requires a 'host' property."),
                source.TcpPort),
            TextQueryTransport.Serial => new SerialTextTransport(
                source.SerialPort ?? throw new InvalidOperationException(
                    $"Serial query source '{source.Name}' requires a 'serial-port' property."),
                source.BaudRate),
            TextQueryTransport.File => new FileTextTransport(
                source.FilePath ?? throw new InvalidOperationException(
                    $"File query source '{source.Name}' requires a 'path' property.")),
            TextQueryTransport.Command => new CommandTextTransport(
                source.Command ?? throw new InvalidOperationException(
                    $"Command query source '{source.Name}' requires a 'command' property.")),
            _ => throw new InvalidOperationException($"Unknown transport: {source.Transport}"),
        };

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        foreach (var transport in _transports)
            await transport.DisposeAsync();
    }
}