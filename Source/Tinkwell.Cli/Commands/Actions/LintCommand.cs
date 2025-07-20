using System.ComponentModel;
using Tinkwell.Cli.Commands.Actions.Lint;
using Tinkwell.Cli.Commands.Lint;

namespace Tinkwell.Cli.Commands.Actions;

[CommandFor("lint", parent: typeof(ActionsCommand))]
[Description("Validate the content of a twa configuration file.")]
sealed class LintCommand : LintCommandBase
{
    protected override IFileLinter CreateLinter()
        => new ActionsLinter();
}
