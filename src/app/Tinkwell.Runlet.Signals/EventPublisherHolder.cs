using Tinkwell.Events;

namespace Tinkwell.Runlet.Signals;

/// <summary>
/// Bridges the <see cref="IEventPublisher"/> created during
/// <see cref="SignalsRunlet.StartAsync"/> with the
/// <see cref="SignalEvaluationWorker"/> which needs it before the
/// publisher is available.
/// </summary>
internal sealed class EventPublisherHolder
{
    private readonly TaskCompletionSource<IEventPublisher> _tcs = new();

    public void Set(IEventPublisher publisher) => _tcs.TrySetResult(publisher);

    public async Task<IEventPublisher> WaitAsync(CancellationToken ct)
    {
        using var reg = ct.Register(() => _tcs.TrySetCanceled(ct));
        return await _tcs.Task;
    }
}
