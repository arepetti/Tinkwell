namespace Tinkwell.Measures.Configuration.Parser;

public class SignalDefinition : ISignalDefinition
{
    public string Name { get; set; } = "";
    public string When { get; set; } = "";
    public string? Topic { get; set; }
    public IDictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();
}
