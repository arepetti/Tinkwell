namespace Tinkwell.Measures.Configuration.Parser;

public sealed class SignalDefinition
{
    public required string Name { get; set; }
    public required string When { get; set; }
    public string? Topic { get; set; }
    public IDictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();
}
