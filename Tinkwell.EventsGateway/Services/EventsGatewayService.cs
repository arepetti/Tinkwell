using Tinkwell.Services;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Grpc.Core;

namespace Tinkwell.EventsGateway.Services;

sealed class EventsGatewayService : Tinkwell.Services.EventsGateway.EventsGatewayBase
{
    public EventsGatewayService(ILogger<EventsGatewayService> logger, IBroker broker)
    {
        _logger = logger;
        _broker = broker;
    }

    public override Task<PublishEventsResponse> Publish(PublishEventsRequest request, ServerCallContext context)
    {
        var eventId = Guid.NewGuid().ToString();
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString() : request.CorrelationId;

        var eventData = new EventData
        {
            Id = eventId,
            Topic = request.Topic,
            Subject = request.Subject,
            Verb = request.Verb.ToString(),
            Object = request.Object,
            Payload = request.Payload,
            CorrelationId = correlationId,
            OccurredAt = (request.OccurredAt ?? Timestamp.FromDateTime(DateTime.UtcNow)).ToDateTime()
        };

        _broker.Publish(eventData);

        return Task.FromResult(new PublishEventsResponse { Id = eventId, CorrelationId = correlationId });
    }

    public override async Task SubscribeTo(SubscribeToEventsRequest request, IServerStreamWriter<SubscribeEventsResponse> responseStream, ServerCallContext context)
    {
        await HandleSubscription(
            responseStream,
            context,
            data => data.Topic.Equals(request.Topic, StringComparison.Ordinal) ? OneMatchWithoutId : NoMatch,
            $"topic = {request.Topic}"
        );
    }

    public override async Task SubscribeToMatching(SubscribeToMatchingEventsRequest request, IServerStreamWriter<SubscribeEventsResponse> responseStream, ServerCallContext context)
    {
        try
        {
            var filter = (EventData data) =>
            {
                if (!_regexCache.IsMatch(data.Topic, request.Topic))
                    return NoMatch;

                if (!_regexCache.IsMatch(data.Subject, request.Subject))
                    return NoMatch;

                if (!_regexCache.IsMatch(data.Verb, request.Verb))
                    return NoMatch;

                if (!_regexCache.IsMatch(data.Object, request.Object))
                    return NoMatch;

                return OneMatchWithoutId;
            };

            await HandleSubscription(responseStream, context, filter, $"pattern/conditional");
        }
        catch (ArgumentException e)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid matching pattern: {e.Message}"));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Call to SubscribeToMatching() failed ({Exception}): {Reason}", e.GetType().Name, e.Message);
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }

    public override async Task SubscribeToMatchingMany(SubscribeToMatchingManyEventsRequest request, IServerStreamWriter<SubscribeEventsResponse> responseStream, ServerCallContext context)
    {
        try
        {
            var filter = (EventData data) =>
            {
                return request.Matches.Where(match =>
                {
                    if (!_regexCache.IsMatch(data.Topic, match.Topic))
                        return false;

                    if (!_regexCache.IsMatch(data.Subject, match.Subject))
                        return false;

                    if (!_regexCache.IsMatch(data.Verb, match.Verb))
                        return false;

                    if (!_regexCache.IsMatch(data.Object, match.Object))
                        return false;

                    return true;
                })
                .Select(x => x.MatchId)
                .ToArray();
            };

            await HandleSubscription(responseStream, context, filter, $"patterns/conditional");
        }
        catch (ArgumentException e)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid matching pattern: {e.Message}"));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Call to SubscribeToMatchingMany() failed ({Exception}): {Reason}", e.GetType().Name, e.Message);
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }
    private static readonly string[] NoMatch = [];
    private static readonly string[] OneMatchWithoutId = [""];
    private readonly ILogger<EventsGatewayService> _logger;
    private readonly IBroker _broker;
    private readonly RegexCache _regexCache = new();

    private async Task HandleSubscription(
        IServerStreamWriter<SubscribeEventsResponse> responseStream,
        ServerCallContext context,
        Func<EventData, string[]> filter,
        string subscriptionIdentifier)
    {
        _logger.LogDebug("Client subscribed to '{Subscription}'", subscriptionIdentifier);
        try
        {
            var tcs = new TaskCompletionSource();

            EventHandler<EnqueuedEventArgs> handler = async (_, args) =>
            {
                try
                {
                    foreach (var matchId in filter(args.Data))
                    {
                        var response = new SubscribeEventsResponse
                        {
                            Id = args.Data.Id,
                            Topic = args.Data.Topic,
                            Subject = args.Data.Subject,
                            Verb = System.Enum.TryParse<Verb>(args.Data.Verb, out var verb) ? verb : Verb.Acted,
                            Object = args.Data.Object,
                            Payload = args.Data.Payload,
                            CorrelationId = args.Data.CorrelationId,
                            OccurredAt = Timestamp.FromDateTime(args.Data.OccurredAt),
                        };

                        if (!string.IsNullOrWhiteSpace(matchId))
                            response.MatchId = matchId;

                        await responseStream.WriteAsync(response);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send update for subscription '{Subscription}'. Client may have disconnected.", subscriptionIdentifier);
                    tcs.TrySetCanceled();
                }
            };

            _broker.Enqueued += handler;
            try
            {
                using var _ = context.CancellationToken.Register(() => tcs.TrySetCanceled());
                await tcs.Task;
            }
            finally
            {
                _broker.Enqueued -= handler;
            }
        }
        catch (OperationCanceledException)
        {
            // Forced cancellation without using the CancellationToken
        }

        _logger.LogDebug("Client unsubscribed from '{Subscription}'", subscriptionIdentifier);
    }
}
