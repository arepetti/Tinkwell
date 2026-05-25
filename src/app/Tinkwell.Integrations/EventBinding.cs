using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Integration;
using Tinkwell.Runner;
using EventsGrpc = Tinkwell.Runlet.Events.Grpc.V1;

namespace Tinkwell.Integration.Events;

/// <summary>
/// Integration binding that publishes an <see cref="EventEnvelope"/> to
/// the event bus. This binding never produces output (returns <see langword="null"/>).
/// </summary>
/// <remarks>
/// <para>Supported parameters:</para>
/// <list type="bullet">
///   <item><c>source</c> — event source (required, string or expression).</item>
///   <item><c>verb</c> — event verb (required, string matching <see cref="EventVerb"/>
///     name or <c>custom:xxx</c> for <see cref="EventVerb.Custom"/>).</item>
///   <item><c>name</c> — event name (required, string or expression).</item>
///   <item><c>object</c> — optional event object.</item>
/// </list>
/// <para>Nested <c>with</c> blocks become <see cref="EventEnvelope.Payload"/> entries.</para>
/// <para>Resolves the event bus via <see cref="IServiceDiscovery"/> (family name <c>events</c>).
/// If the service is unavailable, logs a warning and returns <see langword="null"/> (no output).</para>
/// </remarks>
public sealed class EventBinding : IIntegrationBinding, IMqttIntegrationBinding
{
    private readonly IServiceDiscovery _discovery;
    private readonly ILogger<EventBinding>? _logger;
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private EventsGrpc.EventBus.EventBusClient? _client;

    public EventBinding(IServiceDiscovery discovery, ILogger<EventBinding>? logger = null)
    {
        _discovery = discovery;
        _logger = logger;
    }

    public string Name => "event";

    public Task<BindingResult?> HandleMqttAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct) =>
        HandleAsync(context, parameters, evaluator, ct);

    public async Task<BindingResult?> HandleAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        var expressionParams = context.ToExpressionParameters();

        var source = await BindingParameterResolver.ResolveRequiredAsync("source", "Event", parameters, evaluator, expressionParams, ct);
        var verbRaw = await BindingParameterResolver.ResolveRequiredAsync("verb", "Event", parameters, evaluator, expressionParams, ct);
        var name = await BindingParameterResolver.ResolveRequiredAsync("name", "Event", parameters, evaluator, expressionParams, ct);
        var obj = await BindingParameterResolver.ResolveOptionalAsync("object", parameters, evaluator, expressionParams, ct);

        var (verb, customVerb) = ParseVerb(verbRaw);

        var payload = await BuildPayloadAsync(parameters, evaluator, expressionParams, ct);

        var envelope = new EventEnvelope
        {
            Source = source,
            Verb = verb,
            CustomVerb = customVerb,
            Name = name,
            Object = obj,
            Payload = payload,
        };

        var client = await GetEventBusClientAsync(ct);
        if (client is null)
        {
            _logger?.LogWarning("Event bus not found — skipping publish");
            return null;
        }

        var request = new EventsGrpc.PublishEventRequest
        {
            Source = envelope.Source,
            Verb = (EventsGrpc.EventVerb)(int)envelope.Verb,
            Name = envelope.Name,
            Timestamp = Timestamp.FromDateTime(
                DateTime.SpecifyKind(envelope.Timestamp, DateTimeKind.Utc)),
        };
        if (envelope.CustomVerb is not null)
        {
            request.CustomVerb = envelope.CustomVerb;
        }
        if (envelope.Object is not null)
        {
            request.Object = envelope.Object;
        }
        if (envelope.CorrelationId is not null)
        {
            request.CorrelationId = envelope.CorrelationId;
        }
        foreach (var (k, v) in envelope.Payload)
        {
            request.Payload[k] = v;
        }

        await client.PublishAsync(request, cancellationToken: ct);

        return null;
    }

    private async Task<EventsGrpc.EventBus.EventBusClient?> GetEventBusClientAsync(CancellationToken ct)
    {
        if (_client is not null)
        {
            return _client;
        }

        await _clientLock.WaitAsync(ct);
        try
        {
            if (_client is not null)
            {
                return _client;
            }

            try
            {
                var svc = await _discovery.DiscoverAsync("events", ct);

                if (svc is null)
                {
                    return null;
                }

                _client = await _discovery.CreateInstanceAsync<EventsGrpc.EventBus.EventBusClient>(svc, ct);
                return _client;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to discover event bus");
                return null;
            }
        }
        finally
        {
            _clientLock.Release();
        }
    }

    internal static (EventVerb verb, string? customVerb) ParseVerb(string raw)
    {
        if (raw.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
        {
            return (EventVerb.Custom, raw["custom:".Length..]);
        }

        if (System.Enum.TryParse<EventVerb>(raw, ignoreCase: true, out var verb))
        {
            return (verb, null);
        }

        return (EventVerb.Custom, raw);
    }

    internal static async Task<Dictionary<string, string>> BuildPayloadAsync(
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        IReadOnlyDictionary<string, object?> expressionParams,
        CancellationToken ct)
    {
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, blockProps) in parameters.NestedBlocks)
        {
            foreach (var (key, value) in blockProps)
            {
                var resolved = await BindingParameterResolver.ResolveConfigValueAsync(value, evaluator, expressionParams, ct);
                if (resolved is not null)
                {
                    payload[key] = resolved;
                }
            }
        }

        return payload;
    }
}