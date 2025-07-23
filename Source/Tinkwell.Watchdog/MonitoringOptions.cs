namespace Tinkwell.Watchdog;

sealed class MonitoringOptions
{
    public required TimeSpan Interval { get; init; }
}
