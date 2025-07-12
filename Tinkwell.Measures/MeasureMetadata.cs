namespace Tinkwell.Measures;

/// <summary>
/// Represents metadata for a measure.
/// </summary>
/// <param name="CreatedAt">The timestamp when the measure was created.</param>
public sealed record MeasureMetadata(DateTime CreatedAt)
{
    /// <summary>
    /// Gets or sets the tags associated with the measure.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// If the value is <c>null</c>.
    /// </exception>
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Tags));
            _tags = value;
        }
    }

    /// <summary>
    /// Gets or sets the category of the measure.
    /// </summary>
    public string? Category { get; set; }

    private IReadOnlyList<string> _tags = new List<string>();
}
