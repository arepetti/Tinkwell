using System.ComponentModel;
using System.Text.Json;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Services;

internal sealed class ServiceFindSettings : TwCoordinatorSettings
{
    [Description("Service name, alias, or family name to search for")]
    [CommandArgument(0, "<name>")]
    public string Name { get; set; } = "";
}

[Description("Find a service by name, alias, or family")]
internal sealed class ServiceFindCommand : AsyncCommand<ServiceFindSettings>
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

    public override async Task<int> ExecuteAsync(CommandContext context, ServiceFindSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var data = await output.RunWithStatusAsync(
                $"Looking up service [cyan]{settings.Name}[/]...",
                () => PipeCommandRunner.SendOkAsync(
                    settings, $"service find {settings.Name}", ct));

            if (data is not { ValueKind: JsonValueKind.Object } obj)
            {
                output.WriteError($"Service '{settings.Name}' not found");
                return 1;
            }

            var row = ParseService(obj);
            output.WriteObject($"Service: {row.Name}", Columns, row);
            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    public static ServiceRow ParseService(JsonElement el)
    {
        string? aliases = null;
        if (el.TryGetProperty("aliases", out var aliasesEl) && aliasesEl.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var a in aliasesEl.EnumerateArray())
            {
                var v = a.GetString();
                if (v is not null)
                    list.Add(v);
            }
            if (list.Count > 0)
                aliases = string.Join(", ", list);
        }

        return new ServiceRow(
            Name: el.GetProperty("name").GetString() ?? "?",
            Type: el.TryGetProperty("type", out var t) ? t.GetString() ?? "?" : "?",
            FriendlyName: el.TryGetProperty("friendlyName", out var fn) && fn.ValueKind == JsonValueKind.String
                ? fn.GetString() : null,
            FamilyName: el.TryGetProperty("familyName", out var fam) && fam.ValueKind == JsonValueKind.String
                ? fam.GetString() : null,
            Aliases: aliases,
            Host: el.GetProperty("host").GetString() ?? "?",
            Url: el.GetProperty("url").GetString() ?? "?");
    }
}

internal sealed record ServiceRow(
    string Name,
    string Type,
    string? FriendlyName,
    string? FamilyName,
    string? Aliases,
    string Host,
    string Url);
