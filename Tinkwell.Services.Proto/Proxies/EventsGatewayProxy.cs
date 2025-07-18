namespace Tinkwell.Services.Proto.Proxies;

/// <summary>
/// Implement <see cref="IEventsGateway"/>, a simplified interface for the Events Gateway service.
/// </summary>

public sealed class EventsGatewayProxy(ServiceLocator locator) : IEventsGateway
{
    /// <inheritdocs />
    public async Task PublishAsync(PublishEventsRequest request, CancellationToken cancellationToken)
    {
        var client = await GetClient();
        await client.PublishAsync(request, cancellationToken: cancellationToken);
    }

    /// <inheritdocs />
    public async ValueTask<IStreamingResponse<SubscribeEventsResponse>> SubscribeManyAsync(IEnumerable<SubscribeToMatchingManyEventsRequest.Types.Match> matches, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(matches, nameof(matches));

        var request = new SubscribeToMatchingManyEventsRequest();
        request.Matches.AddRange(matches);

        var client = await GetClient();
        return new StreamingResponseProxy<SubscribeEventsResponse>(
            client.SubscribeToMatchingMany(request, cancellationToken: cancellationToken));
    }

    /// <inheritdocs />
    public ValueTask DisposeAsync()
    {
        if (_gateway is not null)
            return _gateway.DisposeAsync();

        return ValueTask.CompletedTask;
    }

    private readonly ServiceLocator _locator = locator;
    private GrpcService<EventsGateway.EventsGatewayClient>? _gateway;

    private async ValueTask<EventsGateway.EventsGatewayClient> GetClient()
    {
        if (_gateway is null)
            _gateway = await _locator.FindEventsGatewayAsync(CancellationToken.None);

        return _gateway.Client;
    }
}
