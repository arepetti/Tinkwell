using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Actions.Abstractions;
using Tinkwell.Runner;
using EventsGrpc = Tinkwell.Runlet.Events.Grpc.V1;

namespace Tinkwell.Runlet.Actions.Handlers;

/// <summary>
/// Built-in handler that publishes a new event to the event bus.
/// Resolves the event bus via <see cref="IServiceDiscovery"/> when needed.
/// </summary>
/// <remarks>
/// Parameters:
/// <list type="bullet">
///   <item><c>source</c> (required) — the event source.</item>
///   <item><c>verb</c> (required) — the event verb (e.g. fired, changed, created).</item>
///   <item><c>name</c> (required) — the event name.</item>
///   <item><c>object</c> (optional) — the event object/value.</item>
/// </list>
/// The original event's <c>CorrelationId</c> is preserved for tracing.
/// </remarks>
internal sealed class CreateEventActionHandler : IActionHandler
{
    private readonly IServiceDiscovery _discovery;
    private readonly ILogger<CreateEventActionHandler> _logger;
    private EventsGrpc.EventBus.EventBusClient? _client;

    public CreateEventActionHandler(
        IServiceDiscovery discovery,
        ILogger<CreateEventActionHandler> logger)
    {
        _discovery = discovery;
        _logger = logger;
    }

    public string Name => "create-event";

    public async Task ExecuteAsync(
        EventEnvelope trigger,
        IReadOnlyDictionary<string, ConfigValue> parameters,
        IExpressionEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        var source = await ActionParameterResolver.ResolveRequiredAsync(
            "source", parameters, trigger, evaluator, cancellationToken);
        var verbStr = await ActionParameterResolver.ResolveRequiredAsync(
            "verb", parameters, trigger, evaluator, cancellationToken);
        var name = await ActionParameterResolver.ResolveRequiredAsync(
            "name", parameters, trigger, evaluator, cancellationToken);
        var obj = await ActionParameterResolver.ResolveOptionalAsync(
            "object", parameters, trigger, evaluator, cancellationToken);

        var verb = Enum.TryParse<EventVerb>(verbStr, ignoreCase: true, out var v)
            ? v : EventVerb.Custom;

        var envelope = new EventEnvelope
        {
            Source = source,
            Verb = verb,
            CustomVerb = verb == EventVerb.Custom ? verbStr : null,
            Name = name,
            Object = obj,
            CorrelationId = trigger.CorrelationId,
            Timestamp = DateTime.UtcNow,
        };

        var client = await GetEventBusClientAsync(cancellationToken);
        if (client is null)
        {
            _logger.LogWarning("Event bus not found — skipping create-event");
            return;
        }

        var request = EventBusRequestFactory.ToPublishRequest(envelope);
        await client.PublishAsync(request, cancellationToken: cancellationToken);

        _logger.LogDebug("create-event: published {Source}.{Name} ({Verb})",
            source, name, verbStr);
    }

    private async Task<EventsGrpc.EventBus.EventBusClient?> GetEventBusClientAsync(CancellationToken ct)
    {
        if (_client is not null)
            return _client;

        try
        {
            var svc = await _discovery.DiscoverAsync("events", ct);

            if (svc is null)
                return null;

            var client = await _discovery.CreateInstanceAsync<EventsGrpc.EventBus.EventBusClient>(svc, ct);
            Interlocked.CompareExchange(ref _client, client, null);
            return _client;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover event bus");
            return null;
        }
    }
}