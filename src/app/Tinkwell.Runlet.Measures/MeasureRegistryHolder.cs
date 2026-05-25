using Tinkwell.Measures;

namespace Tinkwell.Runlet.Measures;

/// <summary>
/// Bridges the async initialization of <see cref="IMeasureRegistry"/>
/// (which requires service discovery) with DI-resolved consumers like
/// the gRPC service. Set during <see cref="MeasuresRunlet.StartAsync"/>.
/// </summary>
internal sealed class MeasureRegistryHolder
{
    private readonly TaskCompletionSource<IMeasureRegistry> _tcs = new();

    public IMeasureRegistry? Registry { get; private set; }

    public void Set(IMeasureRegistry registry)
    {
        Registry = registry;
        _tcs.TrySetResult(registry);
    }

    /// <summary>
    /// Waits until the registry is initialized or the token is cancelled.
    /// </summary>
    public async Task<IMeasureRegistry> WaitAsync(CancellationToken ct)
    {
        using var reg = ct.Register(() => _tcs.TrySetCanceled(ct));
        return await _tcs.Task;
    }
}
