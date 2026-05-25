namespace Tinkwell.Runlet.Store;

internal enum StoreRole { None, Master, Slave }

/// <summary>
/// Shared state registered by the replication runlet to communicate the
/// store's role and readiness to <see cref="Grpc.StateStoreService"/>.
/// When absent from DI, the store operates in standalone mode.
/// </summary>
internal sealed class StoreMode
{
    private readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public StoreRole Role { get; init; }

    /// <summary>
    /// Completes when the slave has finished its first snapshot sync.
    /// For masters / standalone, this is already completed at construction.
    /// </summary>
    public Task ReadyTask => _ready.Task;

    public void SetReady() => _ready.TrySetResult(true);
}
