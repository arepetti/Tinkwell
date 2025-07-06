using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Reducer;

sealed class Measure : MeasureDefinition, IMeasureDependent
{
    public bool Disabled { get; set; }

    public IList<string> Dependencies { get; } = new List<string>();
}