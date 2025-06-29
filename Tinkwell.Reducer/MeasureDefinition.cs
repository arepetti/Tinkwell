using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Reducer;

sealed class MeasureDefinition : IMeasureDefinition
{
    public string Name { get; set; } = "";
    public string QuantityType { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Expression { get; set; } = "";
    public string? Description { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Category { get; set; }
    public int? Precision { get; set; }
    public bool Disabled { get; set; }
    public List<string> Dependencies { get; set; } = new();
}


