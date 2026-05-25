using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tinkwell.Events;
using Tinkwell.Runner;
using Tinkwell.Runlet.Signals.Grpc.V1;
using EventsGrpc = Tinkwell.Runlet.Events.Grpc.V1;

namespace Tinkwell.Runlet.Signals;

/// <summary>
/// Runlet that evaluates signal conditions against measure values and fires
/// events when conditions are met. Loaded into the same gRPC runner as the
/// measures runlet so it can share the <see cref="Tinkwell.Measures.IMeasureRegistry"/>
/// and <see cref="Tinkwell.Expressions.IExpressionEvaluator"/> via DI.
/// </summary>
/// <remarks>
/// Configuration settings (from the <c>.tw</c> file):
/// <list type="bullet">
///   <item><c>path</c> — Path to the signals <c>.tw</c> file. Defaults to
///     the coordinator's own configuration file.</item>
///   <item><c>publish-events</c> — Whether to publish signal events to the
///     event bus. Defaults to <c>true</c>. Set to <c>false</c> to disable
///     event publishing (consumers can still <c>watch</c> signals via gRPC).</item>
///   <item><c>channel-capacity</c> — Bounded channel size for
///     <see cref="SignalEvaluationWorker"/> (default: <c>512</c>).</item>
///   <item><c>channel-full-mode</c> — <see cref="T:System.Threading.Channels.BoundedChannelFullMode"/>
///     when the channel is full (default: <c>DropWrite</c>).</item>
/// </list>
/// </remarks>
public sealed class SignalsRunlet : IGrpcRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var configPath = settings["path"];
        var channelCapacity = int.TryParse(settings["channel-capacity"], out var cap) ? cap : 512;
        var fullMode = settings["channel-full-mode"] is { } fm
            && System.Enum.TryParse<System.Threading.Channels.BoundedChannelFullMode>(fm, true, out var mode)
                ? mode : System.Threading.Channels.BoundedChannelFullMode.DropWrite;
        var publishEvents = settings["publish-events"] is not { } pe
            || !string.Equals(pe, "false", StringComparison.OrdinalIgnoreCase);
        services.AddSingleton(new SignalsRunletOptions(
            configPath, new ChannelConfig(channelCapacity, fullMode), publishEvents));
        services.AddSingleton<SignalRegistry>();
        services.AddSingleton<EventPublisherHolder>();
        services.AddHostedService<SignalEvaluationWorker>();
    }

    public async Task StartAsync(IServiceProvider services, CancellationToken ct)
    {
        var opts = services.GetRequiredService<SignalsRunletOptions>();
        var holder = services.GetRequiredService<EventPublisherHolder>();
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger<SignalsRunlet>();

        if (!opts.PublishEvents)
        {
            logger.LogInformation("Signal event publishing disabled (publish-events = false)");
            holder.Set(NullEventPublisher.Instance);
            return;
        }

        var discovery = services.GetRequiredService<IServiceDiscovery>();

        var initialDelegate = await DiscoverPublishDelegateAsync(discovery, logger, ct);

        var publisher = new ResilientEventPublisher(
            initialDelegate,
            async ct2 => await DiscoverPublishDelegateAsync(discovery, logger, ct2),
            logger);

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
                logger.LogWarning("Event bus service not found — signal events will not be published");
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

    public void MapGrpcServices(IServiceCollection services)
    {
        services.AddSingleton<SignalsGrpcService>();
    }

    public void MapGrpcEndpoints(IGrpcEndpointMapper mapper)
    {
        mapper.MapService<SignalsGrpcService>(opts =>
        {
            opts.FriendlyName = "Signals";
            opts.FamilyName = "signals";
        });
    }
}

internal sealed record SignalsRunletOptions(string? ConfigPath, ChannelConfig ChannelConfig, bool PublishEvents);

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