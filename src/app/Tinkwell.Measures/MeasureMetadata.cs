namespace Tinkwell.Measures;

/// <summary>
/// Optional metadata attached to a measure at registration time.
/// </summary>
public sealed record MeasureMetadata
{
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public string? Description { get; init; }

    public string? Category { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}
