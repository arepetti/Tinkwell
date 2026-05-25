namespace Tinkwell.Health;

/// <summary>
/// Reports <see cref="HealthStatus.Degraded"/> when a bounded channel's
/// utilization exceeds a configurable threshold. The worker that owns the
/// channel calls <see cref="Attach"/> to bind the check to the live
/// <c>ChannelReader.Count</c>.
/// </summary>
public sealed class ChannelBackpressureCheck : IHealthCheck
{
    private readonly int _capacity;
    private readonly double _threshold;
    private Func<int>? _countAccessor;

    public string Name { get; }

    /// <param name="name">Health check name (e.g. "derived-measures").</param>
    /// <param name="capacity">Bounded channel capacity.</param>
    /// <param name="threshold">Utilization ratio (0..1) above which status is Degraded. Default 0.8.</param>
    public ChannelBackpressureCheck(string name, int capacity, double threshold = 0.8)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(threshold, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(threshold, 1);

        Name = name;
        _capacity = capacity;
        _threshold = threshold;
    }

    /// <summary>
    /// Binds this check to a live channel. Typically called from the
    /// worker's constructor: <c>check.Attach(() => _channel.Reader.Count)</c>.
    /// </summary>
    public void Attach(Func<int> countAccessor)
    {
        ArgumentNullException.ThrowIfNull(countAccessor);
        _countAccessor = countAccessor;
    }

    public Task<HealthCheckResult> CheckAsync(CancellationToken ct)
    {
        if (_countAccessor is null)
            return Task.FromResult(HealthCheckResult.Ok);

        int count = _countAccessor();
        double utilization = (double)count / _capacity;

        if (utilization >= _threshold)
        {
            return Task.FromResult(new HealthCheckResult(
                HealthStatus.Degraded,
                $"Channel {utilization * 100:F0}% full ({count}/{_capacity})"));
        }

        return Task.FromResult(HealthCheckResult.Ok);
    }
}
