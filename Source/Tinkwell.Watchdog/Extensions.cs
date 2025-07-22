using System.Collections.Concurrent;
using Tinkwell.Services;

namespace Tinkwell.Watchdog;

static class Extensions
{
    public static ServiceStatus Translate(this HealthCheckResponse.Types.ServingStatus status)
    {
        return status switch
        {
            HealthCheckResponse.Types.ServingStatus.Serving => ServiceStatus.Serving,
            HealthCheckResponse.Types.ServingStatus.Degraded => ServiceStatus.Degraded,
            HealthCheckResponse.Types.ServingStatus.NotServing => ServiceStatus.Crashed,
            _ => ServiceStatus.Unknown
        };
    }

    public static void UpdateOrAdd(this ConcurrentDictionary<string, Snapshot> snapshots, string runnerName, ServiceStatus status)
    {
        if (snapshots.TryGetValue(runnerName, out var snapshot))
        {
            snapshot.Status = status;
            if (snapshot.Quality >= SnapshotQuality.Poor)
                snapshot.Quality = SnapshotQuality.Good;
        }
        else
        {
            snapshots.TryAdd(runnerName, new()
            {
                Runner = new Runner(runnerName, 0, RunnerRole.Firmlet),
                Timestamp = DateTime.UtcNow,
                Quality = SnapshotQuality.Poor,
                Status = status
            });
        }
    }
}
