namespace Tinkwell.Studio.Services;

public enum CoordinatorConnectivity
{
    Unknown,
    Online,
    Offline,
}

public sealed record CoordinatorStatus(
    CoordinatorConnectivity Connectivity,
    TimeSpan? Latency,
    string? LastError,
    DateTimeOffset Timestamp);
