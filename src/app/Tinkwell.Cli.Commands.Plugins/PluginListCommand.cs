using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Package;

namespace Tinkwell.Cli.Commands.Plugins;

public sealed class PluginListSettings : TwSettings;

[CliCommand("plugin", "list", Description = "List installed plugins")]
public sealed class PluginListCommand : AsyncCommand<PluginListSettings>
{
    private static readonly ColumnDef<PluginRow>[] Columns =
    [
        new("Name", r => r.Name),
        new("Version", r => r.Version),
        new("Subtype", r => r.Subtype),
        new("Source", r => r.Source),
        new("Author", r => r.Author, VerboseOnly: true),
        new("AuthorEmail", r => r.AuthorEmail, VerboseOnly: true),
        new("Company", r => r.Company, VerboseOnly: true),
        new("CompanyWebsite", r => r.CompanyWebsite, VerboseOnly: true),
        new("CompanyEmail", r => r.CompanyEmail, VerboseOnly: true),
        new("SupportEmail", r => r.SupportEmail, VerboseOnly: true),
        new("Description", r => r.Description, VerboseOnly: true),
        new("License", r => r.License, VerboseOnly: true),
        new("LicenseUrl", r => r.LicenseUrl, VerboseOnly: true),
        new("Copyright", r => r.Copyright, VerboseOnly: true),
        new("Contributors", r => r.Contributors, VerboseOnly: true),
        new("ProjectWebsite", r => r.ProjectWebsite, VerboseOnly: true),
        new("DocumentationWebsite", r => r.DocumentationWebsite, VerboseOnly: true),
        new("TermsUrl", r => r.TermsUrl, VerboseOnly: true),
    ];

    public override Task<int> ExecuteAsync(
        CommandContext context, PluginListSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        var catalog = new PluginCatalog();
        var plugins = catalog.Plugins;

        bool readManifests = output.Verbose ||
            output.Format is OutputFormat.List or OutputFormat.Jsonl;

        var rows = new List<PluginRow>(plugins.Count);
        foreach (var plugin in plugins)
        {
            PackageManifest? manifest = null;
            if (readManifests)
                manifest = TryReadManifest(plugin.Directory);

            rows.Add(new PluginRow
            {
                Name = plugin.Name,
                Version = plugin.Version.ToString(),
                Subtype = plugin.Subtype ?? manifest?.Subtype,
                Source = DescribeSource(plugin),
                Author = manifest?.Author,
                AuthorEmail = manifest?.AuthorEmail,
                Company = manifest?.Company,
                CompanyWebsite = manifest?.CompanyWebsite,
                CompanyEmail = manifest?.CompanyEmail,
                SupportEmail = manifest?.SupportEmail,
                Description = manifest?.Description,
                License = manifest?.License,
                LicenseUrl = manifest?.LicenseUrl,
                Copyright = manifest?.Copyright,
                Contributors = manifest?.Contributors,
                ProjectWebsite = manifest?.ProjectWebsite,
                DocumentationWebsite = manifest?.DocumentationWebsite,
                TermsUrl = manifest?.TermsUrl,
            });
        }

        IReadOnlyList<ColumnDef<PluginRow>> visibleColumns = output.Format
            is OutputFormat.List or OutputFormat.Jsonl
            ? Columns
            : Columns;

        output.WriteTable("Plugins", visibleColumns, rows);

        return Task.FromResult(0);
    }

    private static PackageManifest? TryReadManifest(string pluginDir)
    {
        var manifestPath = Path.Combine(pluginDir, WellKnownPaths.Manifest);
        if (!File.Exists(manifestPath))
            return null;

        try
        {
            var text = File.ReadAllText(manifestPath);
            return ManifestFormat.Parse(text);
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeSource(PluginEntry plugin)
    {
        var roots = PluginCatalog.GetDefaultPluginRoots();
        if (plugin.SourcePriority < roots.Count)
        {
            var root = roots[plugin.SourcePriority];
            var localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            var userHome = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);

            if (root.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
                return "app-data";
            if (root.StartsWith(userHome, StringComparison.OrdinalIgnoreCase))
                return "user-home";
            if (root.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
                return "app-local";
        }

        if (plugin.SourcePriority == 0)
            return "env";

        return plugin.Directory;
    }

    private sealed class PluginRow
    {
        public required string Name { get; init; }
        public required string Version { get; init; }
        public string? Subtype { get; init; }
        public required string Source { get; init; }
        public string? Author { get; init; }
        public string? AuthorEmail { get; init; }
        public string? Company { get; init; }
        public string? CompanyWebsite { get; init; }
        public string? CompanyEmail { get; init; }
        public string? SupportEmail { get; init; }
        public string? Description { get; init; }
        public string? License { get; init; }
        public string? LicenseUrl { get; init; }
        public string? Copyright { get; init; }
        public string? Contributors { get; init; }
        public string? ProjectWebsite { get; init; }
        public string? DocumentationWebsite { get; init; }
        public string? TermsUrl { get; init; }
    }
}
