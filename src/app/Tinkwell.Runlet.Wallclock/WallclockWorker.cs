using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Runner;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Runlet.Wallclock;

internal sealed class WallclockWorker(
    WallclockConfig config,
    IServiceDiscovery discovery,
    ILogger<WallclockWorker> logger) : BackgroundService
{
    private MeasuresGrpc.Measures.MeasuresClient? _client;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (config.TimestampMeasureName is null && config.WallclockMeasureName is null)
        {
            logger.LogWarning("Wallclock: both measures disabled");
            return;
        }

        for (var attempt=0; attempt < 30 && !stoppingToken.IsCancellationRequested; ++attempt)
        {
            try
            {
                var svc = await discovery.DiscoverAsync("measures", stoppingToken);
                if (svc is not null)
                {
                    _client = await discovery.CreateInstanceAsync<MeasuresGrpc.Measures.MeasuresClient>(svc, stoppingToken);
                    break;
                }
            }
            catch
            {
                /* retry */
            }
            await Task.Delay(1000, stoppingToken);
        }

        if (_client is null)
        {
            logger.LogError("Could not discover measures service — wallclock idle");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(config.IntervalSeconds));
        do
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Wallclock tick failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var c = _client!;
        if (config.TimestampMeasureName is { } ts)
            await UpdateAsync(c, ts, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), ct);
        if (config.WallclockMeasureName is { } wc)
            await UpdateAsync(c, wc, DateTime.Now.TimeOfDay.TotalSeconds, ct);
    }

    static Task UpdateAsync(MeasuresGrpc.Measures.MeasuresClient client, string name, double v, CancellationToken ct) =>
        client.UpdateAsync(
            new MeasuresGrpc.UpdateMeasureRequest { Name = name, Value = new() { Type = "number", NumericValue = v } },
            cancellationToken: ct).ResponseAsync;
}