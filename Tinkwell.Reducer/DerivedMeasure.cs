namespace Tinkwell.Reducer;

public class DerivedMeasure
{
    public required string Name { get; set; }
    public required string QuantityType { get; set; }
    public required string Unit { get; set; }
    public required string Expression { get; set; }
    public string? Description { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Category { get; set; }
    public int? Precision { get; set; }
}