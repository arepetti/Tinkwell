namespace Tinkwell.Measures.Configuration.Parser;

public interface IDependentMeasure
{
    string Name { get; }
    string Expression { get; }
    IList<string> Dependencies { get; }
}
