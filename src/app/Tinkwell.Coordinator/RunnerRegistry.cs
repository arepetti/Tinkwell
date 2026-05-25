using Tinkwell.Coordinator.Configuration;
using Tinkwell.Runner;

namespace Tinkwell.Coordinator;

/// <summary>
/// Thread-safe registry of all runner definitions. Provides lookup by name or ID
/// and produces snapshots for the <c>runners list</c> command.
/// </summary>
internal sealed class RunnerRegistry
{
    private readonly Lock _lock = new();
    private readonly List<RunnerState> _runners;
    private readonly Dictionary<string, RunnerState> _byName;

    public RunnerRegistry(EnsembleConfig config)
    {
        _runners = config.Runners.Select(r => new RunnerState(r)).ToList();
        _byName = _runners.ToDictionary(r => r.Config.Name, StringComparer.Ordinal);
    }

    /// <summary>
    /// The runner definitions in startup order.
    /// </summary>
    public IReadOnlyList<RunnerState> All
    {
        get { lock (_lock) return _runners.ToList(); }
    }

    /// <summary>
    /// Finds a runner by the short hex ID assigned by the coordinator.
    /// </summary>
    public RunnerState? FindById(string id)
    {
        lock (_lock)
            return _runners.FirstOrDefault(r => r.Id == id);
    }

    /// <summary>
    /// Finds a runner by its configuration name.
    /// </summary>
    public RunnerState? FindByName(string name)
    {
        lock (_lock)
            return _byName.GetValueOrDefault(name);
    }

    /// <summary>
    /// Signals all runners currently waiting for <c>notify ready</c> to unblock.
    /// </summary>
    public void UnblockAll()
    {
        lock (_lock)
        {
            foreach (var runner in _runners)
            {
                if (runner.Status == RunnerStatus.WaitingForReady)
                    runner.SignalUnblock();
            }
        }
    }

    /// <summary>
    /// Produces a snapshot of all runners for the <c>runners list</c> command.
    /// </summary>
    public IReadOnlyList<RunnerInfo> Snapshot()
    {
        lock (_lock)
            return _runners.Select(r => r.ToInfo()).ToList();
    }
}
