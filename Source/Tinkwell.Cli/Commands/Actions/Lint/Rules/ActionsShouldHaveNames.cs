using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Cli.Commands.Lint;

namespace Tinkwell.Cli.Commands.Ensamble.Lint.Rules;

[Linter.Rule(strict: true)]
sealed class ActionsShouldHaveNames : Linter.Rule, ITwaLinterRule<WhenDefinition>
{
    public IEnumerable<Linter.Issue?> Apply(ITwaFile file, object? parent, WhenDefinition item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
            return [Minor<WhenDefinition>(item.Topic, "Actions should have a friendly descriptive name (use the 'name' property).")];

        return None();
    }
}
