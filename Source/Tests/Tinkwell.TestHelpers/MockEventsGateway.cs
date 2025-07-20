using System.Threading.Channels;
using Tinkwell.Services;
using Tinkwell.Services.Proto.Proxies;

namespace Tinkwell.TestHelpers;

/// <summary>
/// <strong>Important</strong>: this mock does not support filters when subscribing to events,
/// all events published will be streamed to all the subscribers.
/// </summary>
public class MockEventsGateway : IEventsGateway
{
    private List<SubscribeToMatchingManyEventsRequest.Types.Match> _matches = new();

    public Task PublishAsync(PublishEventsRequest request, CancellationToken cancellationToken)
    {
        PublishedEvents.Add(request);

        if (Subscribers > 0)
            _channel.Writer.TryWrite(request);

        return Task.CompletedTask;
    }

    public int Subscribers { get; private set; }

    public List<PublishEventsRequest> PublishedEvents { get; private set; } = new();

    public ValueTask<IStreamingResponse<SubscribeEventsResponse>> SubscribeManyAsync(IEnumerable<SubscribeToMatchingManyEventsRequest.Types.Match> matches, CancellationToken cancellationToken)
    {
        ++Subscribers;
        _matches.AddRange(matches);
        return new ValueTask<IStreamingResponse<SubscribeEventsResponse>>(
            new StreamingResponse(_channel.Reader, _matches));
    }

    public ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private readonly Channel<PublishEventsRequest> _channel = Channel.CreateUnbounded<PublishEventsRequest>();
}

#pragma warning disable CS8425 // Async-iterator member has one or more parameters of type 'CancellationToken' but none of them is decorated with the 'EnumeratorCancellation' attribute, so the cancellation token parameter from the generated 'IAsyncEnumerable<>.GetAsyncEnumerator' will be unconsumed

file sealed class StreamingResponse : IStreamingResponse<SubscribeEventsResponse>
{
    private readonly List<SubscribeToMatchingManyEventsRequest.Types.Match> _matches;

    public StreamingResponse(ChannelReader<PublishEventsRequest> reader, List<SubscribeToMatchingManyEventsRequest.Types.Match> matches)
    {
        _reader = reader;
        _matches = matches;
    }

    public async IAsyncEnumerable<SubscribeEventsResponse> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var request in _reader.ReadAllAsync(cancellationToken))
        {
            var match = _matches.FirstOrDefault(m => m.Topic == request.Topic);
            if (match != null)
            {
                yield return new SubscribeEventsResponse
                {
                    Id = Guid.NewGuid().ToString(),
                    Topic = request.Topic,
                    Subject = request.Subject,
                    Verb = request.Verb,
                    Object = request.Object,
                    Payload = request.Payload ?? string.Empty,
                    CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString(),
                    OccurredAt = request.OccurredAt,
                    MatchId = match.MatchId
                };
            }
        }
    }

    public void Dispose() { }

    private readonly ChannelReader<PublishEventsRequest> _reader;
}

#pragma warning restore CS8425