using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Cli.Commands.Lint.Rules;

namespace Tinkwell.Cli.Commands.Ensamble.Lint.Rules;

sealed class RunnerNameAllowedChars : NameAllowedCharsLinerRuleBase, IEnsambleLinterRule<RunnerDefinition>
{
    public Linter.Issue? Apply(IEnsambleFile file, object? parent, RunnerDefinition item)
    {
        // Special case for auto-generated names by Compose. They do not respect the naming rules
        // because we want them to be unique and we need to be sure that no user-defined name could collide.
        // The outer host has the user-defined name then the validation can be done there.
        if (!item.Name.StartsWith("__@", StringComparison.Ordinal))
            Validate(nameof(RunnerDefinition), item.Name);

        return null;
    }
}
