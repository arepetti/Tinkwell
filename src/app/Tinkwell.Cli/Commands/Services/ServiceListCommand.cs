using System.ComponentModel;
using System.Text.Json;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Services;

internal sealed class ServiceListSettings : TwCoordinatorSettings
{
    [Description("Optional filter on name, alias, or family")]
    [CommandOption("--query|-q")]
    public string? Query { get; set; }
}

[Description("List registered services")]
internal sealed class ServiceListCommand : AsyncCommand<ServiceListSettings>
{
    private static readonly IReadOnlyList<ColumnDef<ServiceRow>> Columns =
    [
        new("Name",          s => s.Name),
        new("Type",          s => s.Type),
        new("Host",          s => s.Host),
        new("URL",           s => s.Url),
        new("Friendly Name", s => s.FriendlyName ?? "-", VerboseOnly: true),
        new("Family",        s => s.FamilyName ?? "-",   VerboseOnly: true),
        new("Aliases",       s => s.Aliases ?? "-",      VerboseOnly: true),
    ];

    public override async Task<int> ExecuteAsync(CommandContext context, ServiceListSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var command = string.IsNullOrWhiteSpace(settings.Query)
                ? "service list"
                : $"service list {settings.Query}";

            var data = await output.RunWithStatusAsync(
                "Fetching services...",
                () => PipeCommandRunner.SendOkAsync(settings, command, ct));

            if (data is not { ValueKind: JsonValueKind.Object } obj)
            {
                output.WriteTable("Services", Columns, Array.Empty<ServiceRow>());
                return 0;
            }

            var services = new List<ServiceRow>();
            if (obj.TryGetProperty("services", out var servicesEl) &&
                servicesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in servicesEl.EnumerateArray())
                    services.Add(ServiceFindCommand.ParseService(el));
            }

            output.WriteTable("Services", Columns, services);
            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }
}
