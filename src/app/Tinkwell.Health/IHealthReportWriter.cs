namespace Tinkwell.Health;

/// <summary>
/// Persists a <see cref="HealthReport"/> for a runner. The default
/// implementation writes JSON to the state store.
/// </summary>
public interface IHealthReportWriter
{
    Task WriteAsync(string runnerName, HealthReport report, CancellationToken ct);
}
