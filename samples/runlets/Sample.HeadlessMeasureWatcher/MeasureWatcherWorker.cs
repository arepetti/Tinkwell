using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Tinkwell.Runner;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc;

namespace Sample.HeadlessMeasureWatcher;

/// <summary>
/// Background worker that connects to the Tinkwell Measures gRPC service,
/// opens a <c>Watch</c> server-streaming call, and prints every value change
/// to the console (raw stdout, not the logger).
/// </summary>
internal sealed class MeasureWatcherWorker(
    IServiceDiscovery discovery,
    MeasureWatcherOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine(options.Prefix is null
            ? "[MeasureWatcher] Watching all measures..."
            : $"[MeasureWatcher] Watching measures matching prefix '{options.Prefix}'...");

        MeasuresGrpc.Measures.MeasuresClient? client = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                client ??= await ConnectAsync(stoppingToken);
                if (client is null)
                {
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }

                using var call = client.Watch(
                    new MeasuresGrpc.WatchMeasuresRequest(),
                    cancellationToken: stoppingToken);

                await foreach (var evt in call.ResponseStream.ReadAllAsync(stoppingToken))
                {
                    if (options.Prefix is not null
                        && !evt.Name.StartsWith(options.Prefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var oldDisplay = FormatValue(evt.OldValue);
                    var newDisplay = FormatValue(evt.NewValue);
                    Console.WriteLine($"[MeasureWatcher] {evt.Name}: {oldDisplay} -> {newDisplay}");
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
            {
                Console.WriteLine("[MeasureWatcher] Measures service unavailable, retrying...");
                client = null;
                await Task.Delay(3000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<MeasuresGrpc.Measures.MeasuresClient?> ConnectAsync(CancellationToken ct)
    {
        var svc = await discovery.DiscoverAsync("measures", ct);

        if (svc is null)
            return null;

        return await discovery.CreateInstanceAsync<MeasuresGrpc.Measures.MeasuresClient>(svc, ct);
    }

    private static string FormatValue(MeasuresGrpc.MeasureValueProto? value)
    {
        if (value is null)
            return "(none)";
        if (!string.IsNullOrEmpty(value.StringValue))
            return value.StringValue;
        var num = value.NumericValue.ToString("G");
        return string.IsNullOrEmpty(value.Unit) ? num : $"{num} {value.Unit}";
    }
}
