namespace Tinkwell.HealthCheck;

public sealed class DataSample
{
    public required double CpuUsage { get; init; }
    public required long AllocatedMemory { get; init; }
    public required int ThreadCount { get; init; }
    public required int HandleCount { get; init; }
}
