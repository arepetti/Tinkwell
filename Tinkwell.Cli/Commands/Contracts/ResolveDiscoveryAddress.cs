using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Contracts;

[CommandFor("resolve-discovery-address", parent: typeof(ContractsCommand))]
[Description("Find the service with the specified name.")]
sealed class ResolveDiscoveryAddress : AsyncCommand<ContractsCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ContractsCommand.Settings settings)
    {
        AnsiConsole.WriteLine(await DiscoveryHelpers.ResolveDiscoveryServiceAddressAsync(settings));
        return ExitCode.Ok;
    }
}