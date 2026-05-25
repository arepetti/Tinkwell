using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;

namespace Tinkwell.Cli.Commands.Plugins;

public sealed class PluginInfoSettings : TwSettings
{
    [Description("Registry package name (handle/plugin-name or handle/plugin-name@version)")]
    [CommandArgument(0, "<name>")]
    public string Name { get; set; } = "";

    [Description("Plugin registry URL (overrides TW_REGISTRY_URL and config)")]
    [CommandOption("--registry-url")]
    public string? RegistryUrl { get; set; }

    [Description("Path to the registry's public key file")]
    [CommandOption("--registry-key")]
    public string? RegistryKeyFile { get; set; }
}

[CliCommand("plugin", "info", Description = "Show details about a registry plugin")]
public sealed class PluginInfoCommand : AsyncCommand<PluginInfoSettings>
{
    private static readonly ColumnDef<VersionRow>[] VersionColumns =
    [
        new("Version", r => r.Version),
        new("Architecture", r => r.Architecture),
        new("Verified", r => r.Verified ? "yes" : "no"),
        new("Size", r => FormatSize(r.Size)),
        new("Published", r => r.PublishDate),
    ];

    public override async Task<int> ExecuteAsync(
        CommandContext context, PluginInfoSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        if (!PluginInstallCommand.TryParseRegistryName(settings.Name, out var handle, out var pluginName, out var version))
        {
            output.WriteError("Expected format: handle/plugin-name or handle/plugin-name@version");
            return 1;
        }

        var registryUrl = RegistryConfig.ResolveUrl(settings.RegistryUrl);
        if (string.IsNullOrWhiteSpace(registryUrl))
        {
            output.WriteError("Registry URL not specified. Set --registry-url, TW_REGISTRY_URL, or configure registry.json.");
            return 1;
        }

        try
        {
            using var client = new PluginRegistryClient(registryUrl);

            var filter = $"name=={pluginName}";
            if (version is not null)
                filter += $",version=={version}";

            var result = await output.RunWithStatusAsync("Querying registry...",
                () => client.SearchAsync(filter, "-publishDate", 100, ct));

            var matching = new List<JsonElement>();
            foreach (var item in result.Items)
            {
                if (item.TryGetProperty("author", out var authorEl) &&
                    authorEl.GetString()?.Equals(handle, StringComparison.OrdinalIgnoreCase) == true)
                {
                    matching.Add(item);
                }
            }

            if (matching.Count == 0)
            {
                output.WriteError($"Package '{settings.Name}' not found in the registry.");
                return 1;
            }

            if (output.Format == OutputFormat.Jsonl)
            {
                foreach (var item in matching)
                    Console.WriteLine(item.GetRawText());
                return 0;
            }

            var first = matching[0];
            var desc = first.TryGetProperty("description", out var d) ? d.GetString() : null;
            var license = first.TryGetProperty("license", out var l) ? l.GetString() : null;
            var reqTw = first.TryGetProperty("requiredTinkwellVersion", out var r) ? r.GetString() : null;

            AnsiConsole.MarkupLine($"[bold]{handle}/{pluginName}[/]");
            if (!string.IsNullOrWhiteSpace(desc))
                AnsiConsole.MarkupLine($"  {Markup.Escape(desc)}");
            AnsiConsole.WriteLine();

            if (!string.IsNullOrWhiteSpace(license))
                AnsiConsole.MarkupLine($"  License: [dim]{Markup.Escape(license)}[/]");
            if (!string.IsNullOrWhiteSpace(reqTw))
                AnsiConsole.MarkupLine($"  Requires Tinkwell: [dim]{Markup.Escape(reqTw)}[/]");
            AnsiConsole.WriteLine();

            var rows = matching.Select(item => new VersionRow(
                Version: item.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "",
                Architecture: item.TryGetProperty("architecture", out var a) ? a.GetString() ?? "" : "",
                Verified: item.TryGetProperty("verified", out var vf) && vf.GetBoolean(),
                Size: item.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                PublishDate: item.TryGetProperty("publishDate", out var pd)
                    ? pd.GetDateTimeOffset().ToString("yyyy-MM-dd") : ""
            )).ToList();

            output.WriteTable("Available versions", VersionColumns, rows);
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

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private sealed record VersionRow(
        string Version, string Architecture, bool Verified, long Size, string PublishDate);
}