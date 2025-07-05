using Spectre.Console;
using Tinkwell.Services;

namespace Tinkwell.Cli.Commands.Contracts;

static class Reporter
{
    public static void PrintToConsole(IEnumerable<ServiceDescription> services, bool verbose)
    {
        if (verbose)
            PrintVerbose(services);
        else
            PrintCompact(services);
    }

    private static void PrintCompact(IEnumerable<ServiceDescription> services)
    {
        var table = new Table();
        table.Border = TableBorder.Simple;
        table.AddColumns("[cyan]Name[/]/[darkcyan]Family name[/]", "[yellow]Host[/]");

        foreach (var service in services)
        {
            // This rule is a bit odd but services made to be discovered only by family name use parenthesis to
            // append their runner name to the "normal" service name (see for example HealthCheck).
            bool displayFamilyName = !string.IsNullOrWhiteSpace(service.FamilyName) && service.Name.Contains('(');
            string name = displayFamilyName ? service.FamilyName : service.Name;
            string color = displayFamilyName ? "darkcyan" : "cyan";
            table.AddRow(
                $"[{color}]{name.EscapeMarkup()}[/]",
                service.Host.EscapeMarkup()
            );
        }

        AnsiConsole.Write(table);
    }

    private static void PrintVerbose(IEnumerable<ServiceDescription> services)
    {
        var table = new PropertyValuesTable();
        int index = 1;
        int count = services.Count();
        foreach (var service in services)
        {
            table
                .AddNote($"Service {index++} of {count}")
                .AddNameEntry(service.Name)
                .AddEntry("Friendly name", service.FriendlyName)
                .AddEntry("Family name", service.FamilyName)
                .AddEntry("Aliases", string.Join(',', service.Aliases))
                .AddEntry("Host", service.Host)
                .AddEntry("Endpoint", service.Url)
                .AddRow();
        }
        AnsiConsole.Write(table.ToSpectreTable());
    }
}
