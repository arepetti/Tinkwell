namespace Tinkwell.Watchdog;

sealed class MonitoringOptions
{
    public required TimeSpan Interval { get; init; }
    public required string NamePattern {  get; init; }
}
