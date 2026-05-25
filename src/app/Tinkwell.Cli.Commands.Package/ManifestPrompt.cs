using Spectre.Console;
using Tinkwell.Package;

namespace Tinkwell.Cli.Commands.Package;

/// <summary>
/// Shared logic for collecting manifest properties interactively and
/// building a <see cref="PackageManifest"/> from a dictionary of values.
/// Used by both <c>tw package create-manifest</c> and <c>tw package pack --from-content</c>.
/// </summary>
internal static class ManifestPrompt
{
    internal static readonly (string Key, string Prompt)[] KnownProperties =
    [
        ("name", "Package name"),
        ("type", "Package type (e.g. plugin, firmlet)"),
        ("version", "Version"),
        ("author", "Author"),
        ("author-email", "Author email"),
        ("company", "Company"),
        ("company-website", "Company website"),
        ("company-email", "Company email"),
        ("support-email", "Support email"),
        ("description", "Description"),
        ("license", "License"),
        ("license-url", "License URL"),
        ("copyright", "Copyright"),
        ("contributors", "Contributors"),
        ("project-website", "Project website"),
        ("documentation-website", "Documentation website"),
        ("terms-url", "Terms & Conditions URL"),
    ];

    internal static void PromptInteractively(Dictionary<string, string> values)
    {
        foreach (var (key, prompt) in KnownProperties)
        {
            var value = AnsiConsole.Prompt(
                new TextPrompt<string>($"[bold]{prompt}[/]:")
                    .AllowEmpty());

            if (!string.IsNullOrEmpty(value))
                values[key] = value;
        }
    }

    internal static void ReadFromStdin(Dictionary<string, string> values)
    {
        foreach (var (key, _) in KnownProperties)
        {
            var line = Console.ReadLine();
            if (line is null)
                break;

            if (!string.IsNullOrEmpty(line))
                values[key] = line;
        }

        while (true)
        {
            var line = Console.ReadLine();
            if (string.IsNullOrEmpty(line))
                break;

            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                values[key] = value;
        }
    }

    internal static PackageManifest BuildManifest(Dictionary<string, string> values)
    {
        var extra = new Dictionary<string, string>();
        var knownSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, _) in KnownProperties)
            knownSet.Add(key);

        foreach (var (key, value) in values)
        {
            if (!knownSet.Contains(key))
                extra[key] = value;
        }

        return new PackageManifest
        {
            Name = values.GetValueOrDefault("name") ?? "",
            FormatVersion = 1,
            Type = values.GetValueOrDefault("type"),
            Version = values.GetValueOrDefault("version"),
            Author = values.GetValueOrDefault("author"),
            AuthorEmail = values.GetValueOrDefault("author-email"),
            Company = values.GetValueOrDefault("company"),
            CompanyWebsite = values.GetValueOrDefault("company-website"),
            CompanyEmail = values.GetValueOrDefault("company-email"),
            SupportEmail = values.GetValueOrDefault("support-email"),
            Description = values.GetValueOrDefault("description"),
            License = values.GetValueOrDefault("license"),
            LicenseUrl = values.GetValueOrDefault("license-url"),
            Copyright = values.GetValueOrDefault("copyright"),
            Contributors = values.GetValueOrDefault("contributors"),
            ProjectWebsite = values.GetValueOrDefault("project-website"),
            DocumentationWebsite = values.GetValueOrDefault("documentation-website"),
            TermsUrl = values.GetValueOrDefault("terms-url"),
            Properties = extra,
        };
    }
}
