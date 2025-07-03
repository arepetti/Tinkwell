using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Services;

[CommandFor("services")]
[Description("Query registered services.")]
sealed class ServicesCommand : Command<ServicesCommand.Settings>
{
    public class Settings : CommonSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}