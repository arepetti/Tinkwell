using System.Diagnostics;

namespace Tinkwell.Watchdog;

[DebuggerDisplay("{Runner}")]
sealed class Snapshot
{
    public required Runner Runner { get; init; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow; // Overridden but when empty the JSON transcoding will fail if not UTC

    public SnapshotQuality Quality { get; set; } = SnapshotQuality.Error;

    public ServiceStatus Status { get; set; } = ServiceStatus.Undefined;

    public double CpuUsage { get; set; }

    public double Memory { get; set; }

    public double PeakMemory { get; set; }

    public int ThredCount { get; set; }

    public int HandleCount { get; set; }
}
