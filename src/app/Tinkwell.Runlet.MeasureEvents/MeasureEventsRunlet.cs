using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tinkwell.Events;
using Tinkwell.Runner;
using EventsGrpc = Tinkwell.Runlet.Events.Grpc.V1;

namespace Tinkwell.Runlet.MeasureEvents;

/// <summary>
/// Minimal optional runlet that bridges all measure value-change events
/// to the generic event bus. No configuration, no filters — load it to
/// enable the bridge.
/// </summary>
public sealed class MeasureEventsRunlet : IGrpcRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var channelCapacity = int.TryParse(settings["channel-capacity"], out var cap) ? cap : 4096;
        var fullMode = settings["channel-full-mode"] is { } fm
            && System.Enum.TryParse<System.Threading.Channels.BoundedChannelFullMode>(fm, true, out var mode)
                ? mode : System.Threading.Channels.BoundedChannelFullMode.DropWrite;
        services.AddSingleton(new MeasureEventsOptions(new ChannelConfig(channelCapacity, fullMode)));
        services.AddSingleton<EventPublisherHolder>();
        services.AddHostedService<MeasureEventsWorker>();
    }

    public async Task StartAsync(IServiceProvider services, CancellationToken ct)
    {
        var discovery = services.GetRequiredService<IServiceDiscovery>();
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger<MeasureEventsRunlet>();

        var initialDelegate = await DiscoverPublishDelegateAsync(discovery, logger, ct);

        var publisher = new ResilientEventPublisher(
            initialDelegate,
            async ct2 => await DiscoverPublishDelegateAsync(discovery, logger, ct2),
            logger);

        var holder = services.GetRequiredService<EventPublisherHolder>();
        holder.Set(publisher);
    }

    private static async Task<Func<EventEnvelope, CancellationToken, Task>?> DiscoverPublishDelegateAsync(
        IServiceDiscovery discovery, ILogger logger, CancellationToken ct)
    {
        try
        {
            var svc = await discovery.DiscoverAsync("events", ct);

            if (svc is null)
            {
                logger.LogWarning("Event bus service not found — measure events will not be published");
                return null;
            }

            var client = await discovery.CreateInstanceAsync<EventsGrpc.EventBus.EventBusClient>(svc, ct);
            logger.LogDebug("Event bus discovered at {Url}", svc.Url);

            return async (envelope, ct2) =>
            {
                var request = EventBusRequestFactory.ToPublishRequest(envelope);
                await client.PublishAsync(request, cancellationToken: ct2);
            };
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to discover event bus");
            return null;
        }
    }

    public void MapGrpcServices(IServiceCollection services) { }
    public void MapGrpcEndpoints(IGrpcEndpointMapper mapper) { }
}

internal static class EventBusRequestFactory
{
    public static EventsGrpc.PublishEventRequest ToPublishRequest(EventEnvelope envelope)
    {
        var request = new EventsGrpc.PublishEventRequest
        {
            Source = envelope.Source,
            Verb = (EventsGrpc.EventVerb)(int)envelope.Verb,
            Name = envelope.Name,
            Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                DateTime.SpecifyKind(envelope.Timestamp, DateTimeKind.Utc)),
        };

        if (envelope.CustomVerb is not null)
            request.CustomVerb = envelope.CustomVerb;
        if (envelope.Object is not null)
            request.Object = envelope.Object;
        if (envelope.CorrelationId is not null)
            request.CorrelationId = envelope.CorrelationId;
        foreach (var (k, v) in envelope.Payload)
            request.Payload[k] = v;

        return request;
    }
}

internal sealed record MeasureEventsOptions(ChannelConfig ChannelConfig);