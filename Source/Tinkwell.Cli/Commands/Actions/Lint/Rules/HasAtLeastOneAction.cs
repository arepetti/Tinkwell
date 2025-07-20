using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Cli.Commands.Lint;

namespace Tinkwell.Cli.Commands.Ensamble.Lint.Rules;

sealed class HasAtLeastOneAction : Linter.Rule, ITwaLinterRule<WhenDefinition>
{
    public IEnumerable<Linter.Issue?> Apply(ITwaFile file, object? parent, WhenDefinition item)
    {
        if (!item.Actions.Any())
            return [Warning<WhenDefinition>(item.Name ?? item.Topic, "When definition should have at least one action.")];

        return None();
    }
}
