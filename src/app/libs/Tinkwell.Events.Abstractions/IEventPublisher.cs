namespace Tinkwell.Events;

/// <summary>
/// Publishes events to the event bus. Runlets resolve this from DI
/// to fire events without depending on gRPC directly.
/// </summary>
public interface IEventPublisher
{
    /// <summary>Publishes an event to the bus.</summary>
    Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default);
}
