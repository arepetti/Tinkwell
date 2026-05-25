using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tinkwell.Events;
using DomainVerb = Tinkwell.Events.EventVerb;
using ProtoVerb = Tinkwell.Runlet.Events.Grpc.V1.EventVerb;

namespace Tinkwell.Runlet.Events.Grpc.V1;

internal sealed class EventBusGrpcService : EventBus.EventBusBase
{
    private readonly EventFanOut _fanOut;
    private readonly ILogger<EventBusGrpcService> _logger;

    public EventBusGrpcService(
        EventFanOut fanOut,
        ILogger<EventBusGrpcService> logger)
    {
        _fanOut = fanOut;
        _logger = logger;
    }

    public override Task<PublishEventResponse> Publish(
        PublishEventRequest request, ServerCallContext context)
    {
        var envelope = ToDomain(request);
        _fanOut.Publish(envelope);
        return Task.FromResult(new PublishEventResponse());
    }

    public override async Task Subscribe(
        SubscribeRequest request,
        IServerStreamWriter<EventMessage> responseStream,
        ServerCallContext context)
    {
        var filter = new SubscribeFilter
        {
            Source = string.IsNullOrEmpty(request.Source) ? null : request.Source,
            Verbs = request.Verbs.Count > 0
                ? new HashSet<DomainVerb>(request.Verbs.Select(v => SafeVerb(v)))
                : null,
            NamePrefix = string.IsNullOrEmpty(request.NamePrefix) ? null : request.NamePrefix,
        };

        await foreach (var e in _fanOut.SubscribeAsync(filter, context.CancellationToken))
        {
            try
            {
                await responseStream.WriteAsync(ToProto(e), context.CancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogDebug(
                    ex,
                    "Event subscription stream write cancelled; ending subscribe loop");
                break;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogDebug(
                    ex,
                    "Event subscription stream write failed; ending subscribe loop");
                break;
            }
        }
    }

    private static DomainVerb SafeVerb(ProtoVerb proto)
    {
        var candidate = (DomainVerb)(int)proto;
        return System.Enum.IsDefined(candidate) ? candidate : DomainVerb.Custom;
    }

    private static DateTime ToUtc(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Utc => dt,
        DateTimeKind.Local => dt.ToUniversalTime(),
        _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
    };

    private static EventEnvelope ToDomain(PublishEventRequest r) => new()
    {
        Source = r.Source,
        Verb = SafeVerb(r.Verb),
        CustomVerb = string.IsNullOrEmpty(r.CustomVerb) ? null : r.CustomVerb,
        Name = r.Name,
        Object = string.IsNullOrEmpty(r.Object) ? null : r.Object,
        CorrelationId = string.IsNullOrEmpty(r.CorrelationId) ? null : r.CorrelationId,
        Timestamp = r.Timestamp is not null
            ? r.Timestamp.ToDateTime()
            : DateTime.UtcNow,
        Payload = r.Payload.Count > 0
            ? new Dictionary<string, string>(r.Payload, StringComparer.Ordinal)
            : new Dictionary<string, string>(),
    };

    private static EventMessage ToProto(EventEnvelope e)
    {
        var msg = new EventMessage
        {
            Source = e.Source,
            Verb = (ProtoVerb)(int)e.Verb,
            Name = e.Name,
            Timestamp = Timestamp.FromDateTime(ToUtc(e.Timestamp)),
        };

        if (e.CustomVerb is not null)
        {
            msg.CustomVerb = e.CustomVerb;
        }
        if (e.Object is not null)
        {
            msg.Object = e.Object;
        }
        if (e.CorrelationId is not null)
        {
            msg.CorrelationId = e.CorrelationId;
        }

        foreach (var (k, v) in e.Payload)
        {
            msg.Payload[k] = v;
        }

        return msg;
    }
}
