namespace Tinkwell.Watchdog;

enum RunnerRole
{
    Supervisor,
    Firmlet
}

enum ServiceStatus
{
    Undefined,
    Unknown,
    Serving,
    Degraded,
    Crashed
}

enum SnapshotQuality
{
    Error,
    Undetermined,
    Poor,
    Acceptable,
    Good
}
