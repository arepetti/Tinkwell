using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Reactor;

sealed class Signal(string? owner = default) : SignalDefinition, IMeasureDependent
{
    public bool Disabled { get; set; }

    public IList<string> Dependencies { get; set; } = [];

    string IMeasureDependent.Expression => When;

    internal string? Owner { get; set; } = owner;
}