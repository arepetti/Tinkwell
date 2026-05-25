using System.ComponentModel;
using NuGet.Versioning;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Package;

namespace Tinkwell.Cli.Commands.Plugins;

public sealed class PluginUpdateSettings : TwSettings
{
    [Description("Plugin name to update (e.g. my-plugin or handle/my-plugin). Omit for --all.")]
    [CommandArgument(0, "[name]")]
    public string? Name { get; set; }

    [Description("Show available updates without applying them")]
    [CommandOption("--list|-l")]
    [DefaultValue(false)]
    public bool List { get; set; }

    [Description("Update all plugins that have a known registry source")]
    [CommandOption("--all")]
    [DefaultValue(false)]
    public bool All { get; set; }

    [Description("Overwrite if the same version is already installed")]
    [CommandOption("--force")]
    [DefaultValue(false)]
    public bool Force { get; set; }

    [Description("Allow packages without signatures")]
    [CommandOption("--allow-unsigned")]
    [DefaultValue(false)]
    public bool AllowUnsigned { get; set; }

    [Description("Plugin registry URL (overrides per-plugin stored registry URL)")]
    [CommandOption("--registry-url")]
    public string? RegistryUrl { get; set; }

    [Description("Path to the registry's public key file")]
    [CommandOption("--registry-key")]
    public string? RegistryKeyFile { get; set; }

    [Description("GitHub repository for fallback plugin downloads (overrides TW_GITHUB_REPO and config)")]
    [CommandOption("--github-repo")]
    public string? GitHubRepo { get; set; }
}

[CliCommand("plugin", "update", Description = "Check for and apply plugin updates from the registry or GitHub")]
public sealed class PluginUpdateCommand : AsyncCommand<PluginUpdateSettings>
{
    private static readonly ColumnDef<UpdateRow>[] Columns =
    [
        new("Name", r => r.Name),
        new("Installed", r => r.Installed),
        new("Available", r => r.Available ?? ""),
        new("Status", r => r.Status),
        new("Source", r => r.Source, VerboseOnly: true),
    ];

    public override async Task<int> ExecuteAsync(
        CommandContext context, PluginUpdateSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        if (settings.Name is null && !settings.All && !settings.List)
        {
            output.WriteError("Specify a plugin name, --all, or --list.");
            return 1;
        }

        try
        {
        var catalog = new PluginCatalog();
        var plugins = catalog.Plugins;

        string? filterHandle = null;
        string? filterName = null;
        if (settings.Name is not null)
        {
            if (PluginInstallCommand.TryParseRegistryName(settings.Name, out var h, out var n, out _))
            {
                filterHandle = h;
                filterName = n;
            }
            else
            {
                filterName = settings.Name;
            }
        }

        var grouped = plugins
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Version).First(),
                StringComparer.OrdinalIgnoreCase);

        var rows = new List<UpdateRow>();
        var updatable = new List<(PluginEntry Plugin, string RemoteVersion, string InstallSource, string? RegistryUrl, string? GitHubRepo)>();

        foreach (var (name, plugin) in grouped)
        {
            if (filterName is not null && !name.Equals(filterName, StringComparison.OrdinalIgnoreCase))
                continue;

            var installedFrom = PluginInstallCommand.ReadInstalledFrom(plugin.Directory);

            if (installedFrom == "github")
            {
                var ghSource = PluginInstallCommand.ReadGitHubSourceMetadata(plugin.Directory);
                if (ghSource is null)
                {
                    if (filterName is not null || settings.List)
                        rows.Add(new(name, plugin.Version.ToString(), null, "unknown source", ""));
                    continue;
                }

                var repo = settings.GitHubRepo ?? ghSource.Repo;
                string? remoteVersion;
                try
                {
                    using var github = new GitHubReleasesClient(repo);
                    var asset = await github.ResolveAsync(ghSource.PluginName, null, ct);
                    remoteVersion = asset?.Version;
                }
                catch
                {
                    rows.Add(new(name, plugin.Version.ToString(), null, "github error", $"github:{repo}"));
                    continue;
                }

                if (remoteVersion is null)
                {
                    rows.Add(new(name, plugin.Version.ToString(), null, "not found", $"github:{repo}"));
                    continue;
                }

                var hasUpdate = IsNewer(remoteVersion, plugin.Version.ToString());
                rows.Add(new(name, plugin.Version.ToString(), remoteVersion,
                    hasUpdate ? "update available" : "up to date", $"github:{repo}"));

                if (hasUpdate)
                    updatable.Add((plugin, remoteVersion, ghSource.PluginName, null, repo));
            }
            else
            {
                var regSource = PluginInstallCommand.ReadPluginSourceMetadata(plugin.Directory);
                if (regSource is null)
                {
                    if (filterName is not null || settings.List)
                        rows.Add(new(name, plugin.Version.ToString(), null, "unknown source", ""));
                    continue;
                }

                if (filterHandle is not null && !regSource.Handle.Equals(filterHandle, StringComparison.OrdinalIgnoreCase))
                    continue;

                var registryUrl = settings.RegistryUrl ?? regSource.RegistryUrl;

                RegistryPackage? remote;
                try
                {
                    using var client = new PluginRegistryClient(registryUrl);
                    remote = await client.ResolvePackageAsync(regSource.Handle, regSource.Name, null, ct);
                }
                catch
                {
                    rows.Add(new(name, plugin.Version.ToString(), null, "registry error", registryUrl));
                    continue;
                }

                if (remote is null)
                {
                    rows.Add(new(name, plugin.Version.ToString(), null, "not found", registryUrl));
                    continue;
                }

                var hasUpdate = IsNewer(remote.Version, plugin.Version.ToString());
                rows.Add(new(name, plugin.Version.ToString(), remote.Version,
                    hasUpdate ? "update available" : "up to date", registryUrl));

                if (hasUpdate)
                    updatable.Add((plugin, remote.Version, $"{regSource.Handle}/{regSource.Name}", registryUrl, null));
            }
        }

        if (settings.List || (settings.Name is null && !settings.All))
        {
            output.WriteTable("Plugin Updates", Columns, rows);
            return 0;
        }

        if (updatable.Count == 0)
        {
            output.WriteSuccess("All plugins are up to date.");
            return 0;
        }

        int failures = 0;
        foreach (var (plugin, remoteVersion, installSource, registryUrl, githubRepo) in updatable)
        {
            output.WriteMarkup($"Updating [bold]{plugin.Name}[/] {plugin.Version} -> {remoteVersion}...");

            var installSettings = new PluginInstallSettings
            {
                Source = installSource,
                Update = true,
                Force = settings.Force,
                AllowUnsigned = settings.AllowUnsigned,
                RegistryUrl = registryUrl,
                RegistryKeyFile = settings.RegistryKeyFile,
                GitHubRepo = githubRepo,
            };

            var cmd = new PluginInstallCommand();
            var result = await cmd.ExecuteAsync(context, installSettings, ct);
            if (result != 0)
                failures++;
        }

        return failures > 0 ? 1 : 0;
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

    private static bool IsNewer(string remoteVersion, string installedVersion)
    {
        if (SemanticVersion.TryParse(remoteVersion, out var remote) &&
            SemanticVersion.TryParse(installedVersion, out var installed))
            return remote > installed;
        return string.Compare(remoteVersion, installedVersion, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private sealed record UpdateRow(
        string Name, string Installed, string? Available, string Status, string Source);
}