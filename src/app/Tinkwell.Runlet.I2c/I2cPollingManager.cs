using System.Device.I2c;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Runlet.I2c.Configuration;
using Tinkwell.Runner;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Runlet.I2c;

internal sealed class I2cPollingManager : BackgroundService
{
    private readonly I2cRunletOptions _options;
    private readonly IServiceDiscovery _discovery;
    private readonly ILogger<I2cPollingManager> _logger;
    private readonly Lock _devicesLock = new();
    private readonly List<I2cDevice> _devices = [];

    public I2cPollingManager(
        I2cRunletOptions options,
        IServiceDiscovery discovery,
        ILogger<I2cPollingManager> logger)
    {
        _options = options;
        _discovery = discovery;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsLinux())
        {
            _logger.LogError("I2C runlet requires Linux — skipping on {OS}", Environment.OSVersion.Platform);
            return;
        }

        I2cConfig config;
        try
        {
            var parser = new I2cConfigParser(_logger);
            var configPath = _options.ConfigPath
                ?? Environment.GetEnvironmentVariable("TINKWELL_CONFIG");
            if (configPath is null)
            {
                _logger.LogWarning("No I2C configuration path specified");
                return;
            }

            config = await parser.LoadFileAsync(configPath, cancellationToken: stoppingToken);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse I2C configuration");
            return;
        }

        if (config.Buses.Count == 0)
        {
            _logger.LogInformation("No i2c blocks found in configuration");
            return;
        }

        MeasuresGrpc.Measures.MeasuresClient? measuresClient = null;
        for (int attempt=0; attempt < 30 && !stoppingToken.IsCancellationRequested; ++attempt)
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
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Measures service discovery attempt {Attempt} failed", attempt + 1);
            }
            await Task.Delay(1000, stoppingToken);
        }

        if (measuresClient is null)
        {
            _logger.LogError("Could not discover measures service — I2C polling disabled");
            return;
        }

        var tasks = new List<Task>();
        foreach (var bus in config.Buses)
            tasks.Add(PollBusAsync(bus, measuresClient, stoppingToken));

        await Task.WhenAll(tasks);
    }

    private async Task PollBusAsync(
        I2cBusDefinition bus,
        MeasuresGrpc.Measures.MeasuresClient measuresClient,
        CancellationToken ct)
    {
        var deviceHandles = new List<(I2cDevice Device, I2cDeviceDefinition Def)>();

        try
        {
            foreach (var deviceDef in bus.Devices)
            {
                var settings = new I2cConnectionSettings(bus.BusId, deviceDef.Address);
                var device = I2cDevice.Create(settings);
                lock (_devicesLock) { _devices.Add(device); }
                deviceHandles.Add((device, deviceDef));
                _logger.LogInformation(
                    "I2C opened bus {Bus} device 0x{Address:X2}",
                    bus.BusId, deviceDef.Address);
            }
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open I2C devices on bus {Bus}", bus.BusId);
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            foreach (var (device, deviceDef) in deviceHandles)
            {
                foreach (var read in deviceDef.Reads)
                {
                    try
                    {
                        var buffer = new byte[read.Length];
                        device.WriteByte(read.Register);
                        device.Read(buffer);

                        var value = ByteDecoder.Decode(buffer, read.DataType, read.Scale);

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
                        _logger.LogWarning(ex,
                            "Error reading I2C bus {Bus} device 0x{Address:X2} register 0x{Register:X2}",
                            bus.BusId, deviceDef.Address, read.Register);
                    }
                }
            }

            try
            {
                await Task.Delay(bus.PollInterval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        lock (_devicesLock)
        {
            foreach (var device in _devices)
                device.Dispose();
        }
    }
}