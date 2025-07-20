using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Cli.Commands.Lint.Rules;

namespace Tinkwell.Cli.Commands.Ensamble.Lint.Rules;

sealed class RunnerValidExpression : ValidExpression, IEnsambleLinterRule<RunnerDefinition>
{
    public Linter.Issue? Apply(IEnsambleFile file, object? parent, RunnerDefinition item)
    {
        if (string.IsNullOrWhiteSpace(item.Condition))
            return Ok();

        return Validate(nameof(RunnerDefinition), item.Name, item.Condition);
    }
}
