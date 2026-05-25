namespace Tinkwell.Studio.Services;

/// <summary>
/// Marshals continuations back to the UI thread. Each UI host registers its own
/// implementation (WinUI wraps DispatcherQueue.TryEnqueue, tests can use
/// <see cref="SynchronousDispatcher"/>). View models only hold this abstraction so
/// they stay framework-agnostic.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Schedules <paramref name="action"/> to run on the UI thread. Must not throw
    /// when invoked from a background worker, even while the app is shutting down.
    /// </summary>
    void Post(Action action);
}
