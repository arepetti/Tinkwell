using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Contracts;

[CommandFor("contracts")]
[Description("Query registered services.")]
sealed class ContractsCommand : Command<ContractsCommand.Settings>
{
    public class Settings : CommonSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}