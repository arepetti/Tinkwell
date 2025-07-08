using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Cli.Commands.Lint.Rules;

namespace Tinkwell.Cli.Commands.Ensamble.Lint.Rules;

sealed class WhenValidCondition : ValidExpression, ITwaLinterRule<WhenDefinition>
{
    public Linter.Issue? Apply(ITwaFile file, object? parent, WhenDefinition item)
    {
        if (string.IsNullOrWhiteSpace(item.Condition))
            return Ok();

        return Validate(nameof(RunnerDefinition), item.Topic, item.Condition);
    }
}
