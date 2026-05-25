using Microsoft.UI.Dispatching;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.Services;

/// <summary>
/// Posts work back to the WinUI 3 UI thread via <see cref="DispatcherQueue"/>.
/// The dispatcher is captured once (at construction, on the UI thread), so
/// posts from worker threads reuse it without touching TLS.
/// </summary>
public sealed class WinUiDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue _dispatcherQueue =
        DispatcherQueue.GetForCurrentThread()
        ?? throw new InvalidOperationException(
            "WinUiDispatcher must be constructed on the UI thread.");

    public void Post(Action action)
    {
        // When the dispatcher queue is already on the current thread, execute inline
        // to avoid reentrancy surprises. Otherwise enqueue.
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcherQueue.TryEnqueue(() => action());
    }
}
