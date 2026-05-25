using System.ComponentModel;
using System.Text.Json;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;

namespace Tinkwell.Cli.Commands.Plugins;

public sealed class PluginSearchSettings : TwSettings
{
    [Description("Filter expression (e.g. name~sensor,architecture==Linux_x64)")]
    [CommandOption("--filter")]
    [DefaultValue(null)]
    public string? Filter { get; set; }

    [Description("Sort expression (e.g. -publishDate,name)")]
    [CommandOption("--sort")]
    [DefaultValue(null)]
    public string? Sort { get; set; }

    [Description("Page size (1-100, default 20)")]
    [CommandOption("--page-size")]
    [DefaultValue(null)]
    public int? PageSize { get; set; }

    [Description("Auto-paginate through all results")]
    [CommandOption("--all")]
    [DefaultValue(false)]
    public bool All { get; set; }

    [Description("Plugin registry URL (overrides TW_REGISTRY_URL and config)")]
    [CommandOption("--registry-url")]
    public string? RegistryUrl { get; set; }
}

[CliCommand("plugin", "search", Description = "Search the plugin registry")]
public sealed class PluginSearchCommand : AsyncCommand<PluginSearchSettings>
{
    private static readonly ColumnDef<JsonElement>[] Columns =
    [
        new("Id", e => Prop(e, "id")),
        new("Author", e => Prop(e, "author")),
        new("Name", e => Prop(e, "name")),
        new("Version", e => Prop(e, "version")),
        new("Architecture", e => Prop(e, "architecture")),
        new("Verified", e => Prop(e, "verified")),
        new("PublishDate", e => Prop(e, "publishDate")),
        new("Description", e => Prop(e, "description"), VerboseOnly: true),
        new("License", e => Prop(e, "license"), VerboseOnly: true),
        new("RequiredTwVersion", e => Prop(e, "requiredTinkwellVersion"), VerboseOnly: true),
    ];

    public override async Task<int> ExecuteAsync(
        CommandContext context, PluginSearchSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        var registryUrl = RegistryConfig.ResolveUrl(settings.RegistryUrl);
        if (string.IsNullOrWhiteSpace(registryUrl))
        {
            output.WriteError(
                "Registry URL not specified. Set --registry-url, TW_REGISTRY_URL, or configure it in registry.json.");
            return 1;
        }

        using var client = new PluginRegistryClient(registryUrl);

        try
        {
            if (settings.All)
            {
                var all = new List<JsonElement>();

                var page = await output.RunWithStatusAsync("Searching...",
                    () => client.SearchAsync(settings.Filter, settings.Sort, settings.PageSize, ct));
                all.AddRange(page.Items);

                while (page.NextLink is not null)
                {
                    page = await client.GetNextPageAsync(page.NextLink, ct);
                    all.AddRange(page.Items);
                }

                output.WriteTable("Registry Packages", Columns, all);
            }
            else
            {
                var page = await output.RunWithStatusAsync("Searching...",
                    () => client.SearchAsync(settings.Filter, settings.Sort, settings.PageSize, ct));

                output.WriteTable("Registry Packages", Columns, page.Items.ToList());

                if (page.NextLink is not null && !settings.NonInteractive)
                    output.WriteMarkup("[dim]More results available. Use --all to fetch everything.[/]");
            }

            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    private static string? Prop(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var val))
        {
            return val.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => val.GetString(),
                _ => val.GetRawText(),
            };
        }
        return null;
    }
}