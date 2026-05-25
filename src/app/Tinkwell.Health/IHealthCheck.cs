namespace Tinkwell.Health;

/// <summary>
/// A runlet-supplied health check. Register implementations as
/// <see cref="IHealthCheck"/> singletons in DI; the health monitor
/// worker discovers and evaluates them automatically.
/// </summary>
public interface IHealthCheck
{
    string Name { get; }
    Task<HealthCheckResult> CheckAsync(CancellationToken ct);
}
