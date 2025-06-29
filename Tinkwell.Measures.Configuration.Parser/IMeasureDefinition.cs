namespace Tinkwell.Measures.Configuration.Parser;

public interface IMeasureDefinition
{
    public string Name { get; set; }
    public string QuantityType { get; set; }
    public string Unit { get; set; }
    public string Expression { get; set; }
    public string? Description { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public List<string> Tags { get; set; }
    public string? Category { get; set; }
    public int? Precision { get; set; }
}