namespace Tinkwell.Studio.Services;

/// <summary>
/// Runs posted work inline on the calling thread. Useful as a default in tests
/// or background processes that have no real UI thread.
/// </summary>
public sealed class SynchronousDispatcher : IUiDispatcher
{
    public static readonly SynchronousDispatcher Instance = new();

    public void Post(Action action) => action();
}
