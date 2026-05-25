using Tinkwell.Measures.History;

namespace Tinkwell.Runlet.MeasureHistory;

/// <summary>
/// Bridges async initialization of <see cref="IMeasureHistoryStore"/> with
/// DI-resolved consumers. Set during <see cref="MeasureHistoryRunlet.StartAsync"/>.
/// </summary>
internal sealed class MeasureHistoryStoreHolder
{
    private readonly TaskCompletionSource<IMeasureHistoryStore> _tcs = new();

    public IMeasureHistoryStore? Store { get; private set; }

    public void Set(IMeasureHistoryStore store)
    {
        Store = store;
        _tcs.TrySetResult(store);
    }

    /// <summary>
    /// Waits until the store is initialized or the token is cancelled.
    /// </summary>
    public async Task<IMeasureHistoryStore> WaitAsync(CancellationToken ct)
    {
        using var reg = ct.Register(() => _tcs.TrySetCanceled(ct));
        return await _tcs.Task;
    }
}
