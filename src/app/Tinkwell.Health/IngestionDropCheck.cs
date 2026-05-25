namespace Tinkwell.Health;

/// <summary>
/// Reports <see cref="HealthStatus.Degraded"/> when an ingestion pipeline
/// has dropped messages due to backpressure. Attach one or more drop
/// counters with <see cref="AddCounter"/>; the check sums them and reports
/// the total if non-zero.
/// </summary>
public sealed class IngestionDropCheck : IHealthCheck
{
    private readonly List<Func<long>> _counters = [];

    public string Name { get; }

    /// <param name="name">Health check name (e.g. "coap-drops", "mqtt-drops").</param>
    public IngestionDropCheck(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// Registers a counter accessor. The check sums all registered counters
    /// on each evaluation.
    /// </summary>
    public void AddCounter(Func<long> counterAccessor)
    {
        ArgumentNullException.ThrowIfNull(counterAccessor);
        _counters.Add(counterAccessor);
    }

    public Task<HealthCheckResult> CheckAsync(CancellationToken ct)
    {
        long total = 0;
        foreach (var counter in _counters)
            total += counter();

        if (total > 0)
        {
            return Task.FromResult(new HealthCheckResult(
                HealthStatus.Degraded,
                $"{total} message(s) dropped since startup"));
        }

        return Task.FromResult(HealthCheckResult.Ok);
    }
}
