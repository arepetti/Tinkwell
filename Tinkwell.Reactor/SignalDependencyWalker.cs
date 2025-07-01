using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Reactor;

sealed class SignalDependencyWalker : DependencyWalker<Signal>
{
    protected override IEnumerable<string> ResolveDependencies(Signal item)
    {
        var dependencies = base.ResolveDependencies(item);

        // A signal defined inside a measure has an implicit dependency to that measure!
        if (!string.IsNullOrWhiteSpace(item.Owner) && !dependencies.Contains(item.Owner, StringComparer.Ordinal))
            return Enumerable.Concat(dependencies, [item.Owner]);

        return dependencies;
    }
}