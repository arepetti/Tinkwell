using Tinkwell.Events;

namespace Tinkwell.Runlet.Signals;

/// <summary>
/// No-op <see cref="IEventPublisher"/> used when <c>publish-events</c> is
/// disabled. Silently discards all events so the evaluation worker can run
/// without any null checks or conditional logic.
/// </summary>
internal sealed class NullEventPublisher : IEventPublisher
{
    public static readonly NullEventPublisher Instance = new();

    private NullEventPublisher() { }

    public Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default)
        => Task.CompletedTask;
}
