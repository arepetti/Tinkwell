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
        return new ValueTask<IStreamingResponse<SubscribeEventsResponse>>(
            new StreamingResponse(_channel.Reader));
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
    public StreamingResponse(ChannelReader<PublishEventsRequest> reader)
    {
        _reader = reader;
    }

    public async IAsyncEnumerable<SubscribeEventsResponse> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var request in _reader.ReadAllAsync(cancellationToken))
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
            };
        }
    }

    public void Dispose() { }

    private readonly ChannelReader<PublishEventsRequest> _reader;
}

#pragma warning restore CS8425
