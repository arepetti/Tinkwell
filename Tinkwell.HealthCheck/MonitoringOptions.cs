namespace Tinkwell.HealthCheck;

public sealed class MonitoringOptions
{
    public required TimeSpan Interval { get; init; }
    public required int Samples { get; init; }
    public required double EmaAlpha { get; init; }
    public required int MaximumCpuUsage { get; init; }
}
