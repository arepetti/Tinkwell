namespace Tinkwell.Health;

public sealed record ProcessMetrics(
    double CpuPercent,
    long WorkingSetBytes,
    int ThreadCount,
    int HandleCount);
