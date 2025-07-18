using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Contracts;

[CommandFor("list", parent: typeof(ContractsCommand))]
[Description("List all the registered services.")]
sealed class ListCommand : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : ContractsCommand.Settings
    {
        [CommandArgument(0, "[SEARCH]")]
        [Description("An optional case-insensitive partial match to return only the matching services (by name).")]
        public string Search { get; set; } = "";

        [CommandOption("-v|--verbose")]
        [Description("Show a detailed output.")]
        public bool Verbose { get; set; }

        [CommandOption("-h|--host")]
        [Description("Filter the list to include only services exported at the specified address.")]
        public string Host { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.IsOutputForTool)
            return await ExecuteForToolAsync(context, settings);

        var response = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync("Querying...", async ctx =>
            {
                var request = new Tinkwell.Services.DiscoveryListRequest();
                if (!string.IsNullOrWhiteSpace(settings.Search))
                    request.Query = settings.Search;

                var discovery = await DiscoveryHelpers.FindDiscoveryServiceAsync(settings);
                return await discovery.Client.ListAsync(request);
            });

        var services = response.Services.ToArray();
        if (!string.IsNullOrWhiteSpace(settings.Host))
            services = services.Where(x => string.Equals(x.Host, settings.Host, StringComparison.OrdinalIgnoreCase)).ToArray();

        Reporter.PrintToConsole(services, settings.Verbose);

        return ExitCode.Ok;
    }

    private async Task<int> ExecuteForToolAsync(CommandContext context, Settings settings)
    {
        var request = new Tinkwell.Services.DiscoveryListRequest();
        if (!string.IsNullOrWhiteSpace(settings.Search))
            request.Query = settings.Search;

        var discovery = await DiscoveryHelpers.FindDiscoveryServiceAsync(settings);
        var response = await discovery.Client.ListAsync(request);

        var services = response.Services.ToArray();
        if (!string.IsNullOrWhiteSpace(settings.Host))
            services = services.Where(x => string.Equals(x.Host, settings.Host, StringComparison.OrdinalIgnoreCase)).ToArray();

        foreach (var service in services)
            AnsiConsole.WriteLine($"{service.Name} @ {service.Host}");

        return ExitCode.Ok;
    }
}
