using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Ensamble;

[CommandFor("ensamble")]
[Description("Inspect the ensamble configuration file.")]
public sealed class EnsambleCommand : Command<EnsambleCommand.Settings>
{
    public class Settings : CommandSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}
