using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Actions.Configuration.Parser;

public sealed class WhenDefinition : IConditionalDefinition
{
    public string Topic { get; set; } = "";
    public string? Condition { get; set; }
    public string? Subject { get; set; }
    public string? Verb { get; set; }
    public string? Object { get; set; }
    public List<ActionDefinition> Actions { get; set; } = [];
}
