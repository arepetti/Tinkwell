using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Measures;

[CommandFor("measures")]
[Description("Manage and inspect measures and conditions.")]
sealed class MeasuresCommand : Command<MeasuresCommand.Settings>
{
    public class Settings : CommonSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}
