namespace Tinkwell.Events;

/// <summary>
/// <see cref="IEventPublisher"/> backed by a delegate that performs the
/// actual gRPC call. Runlets wire the delegate in DI using the proto client
/// and service discovery, keeping this class free of gRPC dependencies.
/// </summary>
public sealed class GrpcEventPublisher : IEventPublisher
{
    private readonly Func<EventEnvelope, CancellationToken, Task> _publishDelegate;

    public GrpcEventPublisher(Func<EventEnvelope, CancellationToken, Task> publishDelegate)
    {
        _publishDelegate = publishDelegate;
    }

    public Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default) =>
        _publishDelegate(envelope, ct);
}
