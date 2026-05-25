namespace Tinkwell.Health;

public sealed record HealthReport(
    HealthStatus Status,
    ProcessMetrics Process,
    IReadOnlyDictionary<string, HealthCheckResult> Checks,
    DateTime Timestamp);
