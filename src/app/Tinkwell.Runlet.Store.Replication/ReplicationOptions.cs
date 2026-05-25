namespace Tinkwell.Runlet.Store.Replication;

internal sealed record ReplicationOptions
{
    public required StoreRole Role { get; init; }
    public string? MasterAddress { get; init; }
    public int ReconnectSeconds { get; init; } = 5;
}
