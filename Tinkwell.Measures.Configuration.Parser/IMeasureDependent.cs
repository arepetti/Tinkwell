namespace Tinkwell.Measures.Configuration.Parser;

public interface IMeasureDependent
{
    string Name { get; }
    string Expression { get; }
    IList<string> Dependencies { get; }
}
