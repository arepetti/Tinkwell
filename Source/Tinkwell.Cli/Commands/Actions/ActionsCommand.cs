using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Actions;

[CommandFor("actions", alias: "executor")]
[Description("Inspect the actions configuration file.")]
public sealed class ActionsCommand : Command<ActionsCommand.Settings>
{
    public class Settings : CommandSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}
