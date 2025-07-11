using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Runners;

[CommandFor("runners")]
[Description("Manage the active runners.")]
sealed class RunnersCommand : Command<RunnersCommand.Settings>
{
    public class Settings : LiveInstanceCommonSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}