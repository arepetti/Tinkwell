namespace Tinkwell.Health;

public sealed class HealthMonitorOptions
{
    /// <summary>Delay before the first collection in seconds (default: 10).</summary>
    public int InitialDelaySeconds { get; set; } = 10;

    /// <summary>Sampling interval in seconds (default: 60).</summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>Number of samples to keep for EMA averaging (default: 5).</summary>
    public int Samples { get; set; } = 5;

    /// <summary>EMA smoothing factor (0..1). Higher = more weight on recent samples.</summary>
    public double EmaAlpha { get; set; } = 0.7;

    /// <summary>CPU usage threshold (percent) above which status is Degraded.</summary>
    public int CpuThresholdPercent { get; set; } = 90;

    public TimeSpan InitialDelay => TimeSpan.FromSeconds(InitialDelaySeconds);

    public TimeSpan Interval => TimeSpan.FromSeconds(IntervalSeconds);

    /// <summary>TTL for the health report in the store (2x interval).</summary>
    public TimeSpan Ttl => TimeSpan.FromSeconds(IntervalSeconds * 2);
}
