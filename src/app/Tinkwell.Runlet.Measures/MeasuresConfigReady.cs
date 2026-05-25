using Tinkwell.Runlet.Measures.Configuration;

namespace Tinkwell.Runlet.Measures;

/// <summary>
/// Signals that measure definitions have been loaded and registered.
/// <see cref="DerivedMeasureWorker"/> sets this after registration when
/// <c>calculated-measures</c> is enabled; when disabled,
/// <see cref="MeasuresRunlet.StartAsync"/> sets an empty config so that
/// cross-runlet consumers (signals, measure-events) that await
/// <see cref="WaitAsync"/> are not blocked indefinitely.
/// </summary>
internal sealed class MeasuresConfigReady
{
    private readonly TaskCompletionSource<MeasuresConfig> _tcs = new();

    public void Set(MeasuresConfig config) => _tcs.TrySetResult(config);

    public async Task<MeasuresConfig> WaitAsync(CancellationToken ct)
    {
        using var reg = ct.Register(() => _tcs.TrySetCanceled(ct));
        return await _tcs.Task;
    }
}
