using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Measures;

[CommandFor("measures", alias: "reducer")]
[Description("Manage and inspect measures and conditions.")]
sealed class MeasuresCommand : Command<MeasuresCommand.Settings>
{
    public class Settings : LiveInstanceCommonSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}
