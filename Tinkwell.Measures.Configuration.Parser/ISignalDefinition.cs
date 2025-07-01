namespace Tinkwell.Measures.Configuration.Parser;

public interface ISignalDefinition
{
    string Name { get; set; }
    IDictionary<string, object> Payload { get; set; }
    string? Topic { get; set; }
    string When { get; set; }
}
