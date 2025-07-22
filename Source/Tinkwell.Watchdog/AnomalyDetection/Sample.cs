namespace Tinkwell.Watchdog.AnomalyDetection;

sealed record Sample(double Cpu, double Memory, int Threads, int Handles)
{
    public static Sample FromSnapshot(Snapshot snapshot)
        => new Sample(snapshot.CpuUsage, snapshot.Memory, snapshot.ThredCount, snapshot.HandleCount);
}
