using System.Collections.Concurrent;
using Tinkwell.Watchdog.AnomalyDetection;

namespace Tinkwell.Watchdog;

sealed class HealthData
{
    public Snapshot[] GetSnapshots()
        => _snapshots.Values.ToArray();

    public bool? IsLatestSampleAnAnomaly { get; set; }

    public void BeginUpdate()
    {
    }

    public void EndUpdate()
    {
        IsLatestSampleAnAnomaly = _trainer.Enqueue(GetSnapshots());
    }

    public void AddRange(IEnumerable<Runner> runners)
    {
        foreach (var runner in runners)
        {
            var snapshot = new Snapshot { Runner = runner };
            if (!_snapshots.TryAdd(runner.Name, snapshot))
                _snapshots[runner.Name] = snapshot;
        }
    }

    public void UpdateOrAdd(string runnerName, ServiceStatus status)
    {
        if (_snapshots.TryGetValue(runnerName, out var snapshot))
        {
            snapshot.Status = status;
            if (snapshot.Quality >= SnapshotQuality.Poor)
                snapshot.Quality = SnapshotQuality.Good;
        }
        else
        {
            _snapshots.TryAdd(runnerName, new()
            {
                Runner = new Runner(runnerName, 0, RunnerRole.Firmlet),
                Timestamp = DateTime.UtcNow,
                Quality = SnapshotQuality.Poor,
                Status = status
            });
        }
    }

    private readonly ConcurrentDictionary<string, Snapshot> _snapshots = new();
    private readonly Trainer _trainer = new();
}