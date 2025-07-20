namespace Tinkwell.Services.Proto.Proxies;

/// <summary>
/// Simplified interface for the Events Gateway service.
/// </summary>
public interface IEventsGateway : IAsyncDisposable
{
    /// <summary>
    /// Publishes the specified event.
    /// </summary>
    /// <param name="request">Event to publish.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task PublishAsync(PublishEventsRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Subscribes to the specified set of events.
    /// </summary>
    /// <param name="matches">The set of events to subscribe to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The streaming interface you can use to enumerate asynchronously all the events.
    /// </returns>
    ValueTask<IStreamingResponse<SubscribeEventsResponse>> SubscribeManyAsync(IEnumerable<SubscribeToMatchingManyEventsRequest.Types.Match> matches, CancellationToken cancellationToken);
}