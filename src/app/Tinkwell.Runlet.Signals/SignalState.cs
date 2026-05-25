namespace Tinkwell.Runlet.Signals;

/// <summary>
/// Tracks the lifecycle state of a signal instance within the evaluation
/// worker's state machine.
/// </summary>
internal enum SignalState
{
    /// <summary>Waiting for the <c>when</c> condition to become true.</summary>
    Idle,

    /// <summary>
    /// The <c>when</c> condition is true and the <c>for</c> duration timer
    /// is running. Transitions back to <see cref="Idle"/> if the condition
    /// becomes false before the duration elapses.
    /// </summary>
    Pending,

    /// <summary>
    /// The signal has fired. If the signal has an <c>until</c> clause, it
    /// stays in this state (suppressing re-fires) until the <c>until</c>
    /// condition becomes true.
    /// </summary>
    Active,
}
