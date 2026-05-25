using Tinkwell.Runlet.Signals.Configuration;

namespace Tinkwell.Runlet.Signals;

/// <summary>
/// Mutable runtime state for a single signal being tracked by the
/// <see cref="SignalEvaluationWorker"/>.
/// </summary>
internal sealed class SignalInstance
{
    public SignalDefinition Definition { get; }
    public SignalState State { get; set; } = SignalState.Idle;

    /// <summary>
    /// Monotonically increasing sequence number used to invalidate stale
    /// <see cref="DurationElapsed"/> events after a state reset.
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>
    /// Cancellation source for a pending <c>for</c> duration timer.
    /// Non-null only while <see cref="State"/> is <see cref="SignalState.Pending"/>.
    /// </summary>
    public CancellationTokenSource? PendingCts { get; set; }

    /// <summary>
    /// The most recent correlation ID from the measure change that
    /// triggered this signal's current evaluation cycle.
    /// </summary>
    public string? CorrelationId { get; set; }

    public SignalInstance(SignalDefinition definition)
    {
        Definition = definition;
    }

    public void CancelPending()
    {
        var cts = PendingCts;
        PendingCts = null;
        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            cts.Dispose();
        }
    }
}
