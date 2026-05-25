using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Measures;

namespace Tinkwell.Runlet.Measures;

/// <summary>
/// Drives the <see cref="Tinkwell.Measures.IMeasureRegistry.WatchAsync"/>
/// loop so that <see cref="Tinkwell.Measures.IMeasureRegistry.ValueChanged"/>
/// events are raised for all store changes.
/// </summary>
/// <remarks>
/// <para>
/// <c>ValueChanged</c> is only raised from inside <c>WatchAsync</c>, which
/// opens a gRPC streaming call to the state store. If nobody calls
/// <c>WatchAsync</c>, the event never fires — even if measures change
/// externally.
/// </para>
/// <para>
/// This worker is always registered (regardless of <c>calculated-measures</c>)
/// to guarantee that any subscriber to <c>ValueChanged</c> — derived-measure
/// recalculation, signal evaluation, the measure-events bridge, or future
/// consumers — receives events without depending on another worker to drive
/// the stream.
/// </para>
/// </remarks>
internal sealed class MeasureWatchWorker : BackgroundService
{
    private readonly MeasureRegistryHolder _registryHolder;
    private readonly ILogger<MeasureWatchWorker> _logger;

    public MeasureWatchWorker(
        MeasureRegistryHolder registryHolder,
        ILogger<MeasureWatchWorker> logger)
    {
        _registryHolder = registryHolder;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IMeasureRegistry registry;
        try
        {
            registry = await _registryHolder.WaitAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _logger.LogDebug("MeasureWatchWorker started — driving the store watch stream");

        try
        {
            await registry.WatchAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogDebug("Store watch stream cancelled during shutdown");
        }
    }
}
