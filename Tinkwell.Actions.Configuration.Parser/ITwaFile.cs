namespace Tinkwell.Actions.Configuration.Parser;

public interface ITwaFile
{
    IEnumerable<WhenDefinition> Listeners { get; }
}
