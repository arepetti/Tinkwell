using System.ComponentModel;
using Tinkwell.Cli.Commands.Ensamble.Lint;
using Tinkwell.Cli.Commands.Lint;

namespace Tinkwell.Cli.Commands.Ensamble;

[CommandFor("lint", parent: typeof(EnsambleCommand))]
[Description("Validate the content of a tw configuration file.")]
sealed class LintCommand : LintCommandBase
{
    protected override IFileLinter CreateLinter()
        => new EnsambleLinter();
}
