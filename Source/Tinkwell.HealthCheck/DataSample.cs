namespace Tinkwell.HealthCheck;

public sealed class DataSample
{
    public static readonly DataSample Invalid = new()
    {
        CpuUsage = -1,
        AllocatedMemory = -1,
        ThreadCount = -1,
        HandleCount = -1
    };

    public required double CpuUsage { get; init; }
    public required long AllocatedMemory { get; init; }
    public required int ThreadCount { get; init; }
    public required int HandleCount { get; init; }
}
