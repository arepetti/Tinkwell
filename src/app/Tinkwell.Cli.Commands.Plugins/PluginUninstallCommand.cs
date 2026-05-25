using System.ComponentModel;
using System.Text.Json;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;

namespace Tinkwell.Cli.Commands.Plugins;

public sealed class PluginUninstallSettings : TwSettings
{
    [Description("Plugin name or name@version to uninstall")]
    [CommandArgument(0, "<name>")]
    public string Name { get; set; } = "";

    [Description("Remove ALL installed versions (only valid without an explicit @version)")]
    [CommandOption("--all")]
    [DefaultValue(false)]
    public bool All { get; set; }
}

[CliCommand("plugin", "uninstall", Description = "Uninstall a plugin")]
public sealed class PluginUninstallCommand : AsyncCommand<PluginUninstallSettings>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public override Task<int> ExecuteAsync(
        CommandContext context, PluginUninstallSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            ParseTarget(settings.Name, out var pluginName, out var explicitVersion);

            if (explicitVersion is not null && settings.All)
            {
                output.WriteError("Cannot combine --all with an explicit version (name@version).");
                return Task.FromResult(1);
            }

            var installRoot = PluginInstallCommand.GetInstallRoot();
            var catalog = new PluginCatalog([installRoot]);
            var matching = catalog.Plugins
                .Where(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (explicitVersion is not null)
            {
                matching = matching
                    .Where(p => p.Version.Equals(explicitVersion))
                    .ToList();
            }

            if (matching.Count == 0)
            {
                output.WriteError($"Plugin not found: {settings.Name}");
                return Task.FromResult(1);
            }

            List<PluginEntry> toRemove;
            if (settings.All || explicitVersion is not null)
            {
                toRemove = matching;
            }
            else
            {
                toRemove = [matching.OrderByDescending(p => p.Version).First()];
            }

            foreach (var entry in toRemove)
            {
                Directory.Delete(entry.Directory, recursive: true);
            }

            if (output.Format == OutputFormat.Jsonl)
            {
                var json = JsonSerializer.Serialize(new
                {
                    status = "ok",
                    removed = toRemove.Select(e => new
                    {
                        name = e.Name,
                        version = e.Version.ToString(),
                    }).ToArray(),
                    message = toRemove.Count == 1
                        ? $"Uninstalled {toRemove[0].Name}@{toRemove[0].Version}"
                        : $"Uninstalled {toRemove.Count} version(s) of {pluginName}",
                }, JsonOpts);
                Console.WriteLine(json);
            }
            else
            {
                foreach (var entry in toRemove)
                    output.WriteSuccess($"Uninstalled [bold]{entry.Name}@{entry.Version}[/]");
            }

            return Task.FromResult(0);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            output.WriteError(ex.Message);
            return Task.FromResult(1);
        }
    }

    private static void ParseTarget(string input, out string name, out Version? version)
    {
        version = null;
        var atIndex = input.LastIndexOf('@');
        if (atIndex > 0 && atIndex < input.Length - 1 &&
            Version.TryParse(input[(atIndex + 1)..], out var parsed))
        {
            name = input[..atIndex];
            version = parsed;
        }
        else
        {
            name = input;
        }
    }
}