namespace Tinkwell.Runlet.MeasureHistory;

internal sealed record MeasureHistoryOptions
{
    public required string Backend { get; init; }
    public string? ConnectionString { get; init; }
    public int BatchSize { get; init; }
    public int FlushIntervalMs { get; init; }
}
