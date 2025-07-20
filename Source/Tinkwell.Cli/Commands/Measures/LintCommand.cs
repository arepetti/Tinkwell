using System.ComponentModel;
using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Cli.Commands.Measures.Lint;

namespace Tinkwell.Cli.Commands.Measures;

[CommandFor("lint", parent: typeof(MeasuresCommand))]
[Description("Validate the content of a twm configuration file.")]
sealed class LintCommand : LintCommandBase
{
    protected override IFileLinter CreateLinter()
        => new TwmLinter();
}
