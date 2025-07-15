using Tinkwell.Measures;

namespace Tinkwell.Store.Storage.Sqlite;

sealed class MeasureMetadataDto
{
    public required string Name { get; set; }
    public long CreatedAt { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public required string Tags { get; set; }

    public MeasureMetadata ToMeasureMetadata()
    {
        return new MeasureMetadata(DateTimeOffset.FromUnixTimeMilliseconds(CreatedAt).DateTime)
        {
            Description = Description,
            Category = Category,
            Tags = Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? []
        };
    }
}
