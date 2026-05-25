using System.Collections.Concurrent;
using System.Globalization;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Tinkwell.Runner;
using EventsGrpc = Tinkwell.Runlet.Events.Grpc;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc;

namespace Sample.AnomalyDetector;

/// <summary>
/// Watches all measures via <c>Measures.Watch</c>, feeds numeric values into
/// per-measure <see cref="MeasureTracker"/> instances, and publishes a
/// <c>Fired</c> event to the event bus when an anomaly is detected.
/// </summary>
internal sealed class AnomalyDetectorWorker(
    IServiceDiscovery discovery,
    AnomalyDetectorOptions options) : BackgroundService
{
    private readonly ConcurrentDictionary<string, MeasureTracker> _trackers = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine(options.Prefix is null
            ? $"[AnomalyDetector] Watching all measures (threshold={options.Threshold}, window={options.WindowSize})"
            : $"[AnomalyDetector] Watching '{options.Prefix}*' (threshold={options.Threshold}, window={options.WindowSize})");

        MeasuresGrpc.Measures.MeasuresClient? measuresClient = null;
        EventsGrpc.EventBus.EventBusClient? eventsClient = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                measuresClient ??= await CreateClientAsync<MeasuresGrpc.Measures.MeasuresClient>("measures", stoppingToken);
                eventsClient ??= await CreateClientAsync<EventsGrpc.EventBus.EventBusClient>("events", stoppingToken);

                if (measuresClient is null || eventsClient is null)
                {
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }

                using var call = measuresClient.Watch(
                    new MeasuresGrpc.WatchMeasuresRequest(),
                    cancellationToken: stoppingToken);

                await foreach (var evt in call.ResponseStream.ReadAllAsync(stoppingToken))
                {
                    if (options.Prefix is not null
                        && !evt.Name.StartsWith(options.Prefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (evt.NewValue is null)
                        continue;

                    double value = evt.NewValue.NumericValue;
                    var tracker = _trackers.GetOrAdd(evt.Name,
                        _ => new MeasureTracker(options.WindowSize, options.Threshold));

                    var anomaly = tracker.Push(value);
                    if (anomaly is null)
                        continue;

                    Console.WriteLine(
                        $"[AnomalyDetector] ANOMALY: {evt.Name} = {anomaly.Value:G} " +
                        $"(z={anomaly.ZScore:F2}, mean={anomaly.Mean:G}, stddev={anomaly.StdDev:G})");

                    await PublishAnomalyAsync(eventsClient, evt.Name, anomaly, stoppingToken);
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
            {
                Console.WriteLine("[AnomalyDetector] Service unavailable, retrying...");
                measuresClient = null;
                eventsClient = null;
                await Task.Delay(3000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<T?> CreateClientAsync<T>(string family, CancellationToken ct)
        where T : class
    {
        var svc = await discovery.DiscoverAsync(family, ct);
        if (svc is null)
            return null;
        return await discovery.CreateInstanceAsync<T>(svc, ct);
    }

    private static async Task PublishAnomalyAsync(
        EventsGrpc.EventBus.EventBusClient client,
        string measureName,
        AnomalyResult anomaly,
        CancellationToken ct)
    {
        var request = new EventsGrpc.PublishEventRequest
        {
            Source = "anomaly-detector",
            Verb = EventsGrpc.EventVerb.Fired,
            Name = measureName,
            Object = "anomaly",
            Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
        };

        request.Payload.Add("value", anomaly.Value.ToString("G", CultureInfo.InvariantCulture));
        request.Payload.Add("z-score", anomaly.ZScore.ToString("F2", CultureInfo.InvariantCulture));
        request.Payload.Add("mean", anomaly.Mean.ToString("G", CultureInfo.InvariantCulture));
        request.Payload.Add("stddev", anomaly.StdDev.ToString("G", CultureInfo.InvariantCulture));

        try
        {
            await client.PublishAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            Console.WriteLine($"[AnomalyDetector] Failed to publish anomaly event for '{measureName}': events service unavailable");
        }
    }
}
