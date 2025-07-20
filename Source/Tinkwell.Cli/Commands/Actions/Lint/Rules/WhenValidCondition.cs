using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Cli.Commands.Lint.Rules;

namespace Tinkwell.Cli.Commands.Ensamble.Lint.Rules;

sealed class WhenValidCondition : ValidExpression, ITwaLinterRule<WhenDefinition>
{
    public IEnumerable<Linter.Issue?> Apply(ITwaFile file, object? parent, WhenDefinition item)
        => [Validate(nameof(RunnerDefinition), item.Name ?? item.Topic, item.Condition)];
}
