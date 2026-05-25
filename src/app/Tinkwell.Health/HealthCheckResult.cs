namespace Tinkwell.Health;

public sealed record HealthCheckResult(HealthStatus Status, string? Message = null)
{
    public static readonly HealthCheckResult Ok = new(HealthStatus.Healthy);
}
