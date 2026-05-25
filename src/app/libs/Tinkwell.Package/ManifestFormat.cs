using System.Globalization;

namespace Tinkwell.Package;

/// <summary>
/// Parses and writes <c>package.tw</c> manifests.
/// </summary>
public static class ManifestFormat
{
    internal const int MaxNameLength = 512;

    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "format-version", "type", "subtype", "version", "author", "author-email",
        "company", "company-website", "company-email", "support-email",
        "description", "license", "license-url", "copyright", "contributors",
        "project-website", "documentation-website", "terms-url",
    };

    /// <summary>
    /// Parses <paramref name="text"/> as a <c>package</c> block and returns
    /// a <see cref="PackageManifest"/>. Unknown keys are collected into
    /// <see cref="PackageManifest.Properties"/>.
    /// </summary>
    /// <param name="text">Full content of a <c>package.tw</c> file (a single <c>package</c> block in tw format).</param>
    /// <exception cref="PackageException">The text is not a single valid <c>package</c> block, or a required value is missing or invalid.</exception>
    public static PackageManifest Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var block = TwReader.ReadSingleBlock(text);

        if (!block.Type.Equals("package", StringComparison.OrdinalIgnoreCase))
            throw new PackageException(
                $"Expected 'package' block, found '{block.Type}'");

        if (string.IsNullOrWhiteSpace(block.Name))
            throw new PackageException("Package name is required");

        if (block.Name.Length > MaxNameLength)
            throw new PackageException(
                $"Package name exceeds maximum length ({MaxNameLength})");

        if (block.Name.AsSpan().IndexOfAny("\0\r\n") >= 0 || HasControlCharacters(block.Name))
            throw new PackageException(
                "Package name contains invalid characters (control characters are not allowed)");

        var extra = new Dictionary<string, string>();
        foreach (var (key, value) in block.Properties)
        {
            if (!KnownKeys.Contains(key))
                extra[key] = value;
        }

        int formatVersion = GetInt(block, "format-version", 1);
        if (formatVersion < 1)
            throw new PackageException(
                $"Invalid format-version '{formatVersion}': must be at least 1");

        return new PackageManifest
        {
            Name = block.Name,
            FormatVersion = formatVersion,
            Type = GetString(block, "type"),
            Subtype = GetString(block, "subtype"),
            Version = GetString(block, "version"),
            Author = GetString(block, "author"),
            AuthorEmail = GetString(block, "author-email"),
            Company = GetString(block, "company"),
            CompanyWebsite = GetString(block, "company-website"),
            CompanyEmail = GetString(block, "company-email"),
            SupportEmail = GetString(block, "support-email"),
            Description = GetString(block, "description"),
            License = GetString(block, "license"),
            LicenseUrl = GetString(block, "license-url"),
            Copyright = GetString(block, "copyright"),
            Contributors = GetString(block, "contributors"),
            ProjectWebsite = GetString(block, "project-website"),
            DocumentationWebsite = GetString(block, "documentation-website"),
            TermsUrl = GetString(block, "terms-url"),
            Properties = extra,
        };
    }

    /// <summary>
    /// Serializes <paramref name="manifest"/> to the canonical <c>package.tw</c>
    /// text format, including a standard header line.
    /// </summary>
    /// <param name="manifest">Package metadata to serialize.</param>
    public static string Write(PackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var props = new Dictionary<string, string>
        {
            ["format-version"] = manifest.FormatVersion.ToString(),
        };

        if (manifest.Type is not null)
            props["type"] = manifest.Type;
        if (manifest.Subtype is not null)
            props["subtype"] = manifest.Subtype;
        if (manifest.Version is not null)
            props["version"] = manifest.Version;
        if (manifest.Author is not null)
            props["author"] = manifest.Author;
        if (manifest.AuthorEmail is not null)
            props["author-email"] = manifest.AuthorEmail;
        if (manifest.Company is not null)
            props["company"] = manifest.Company;
        if (manifest.CompanyWebsite is not null)
            props["company-website"] = manifest.CompanyWebsite;
        if (manifest.CompanyEmail is not null)
            props["company-email"] = manifest.CompanyEmail;
        if (manifest.SupportEmail is not null)
            props["support-email"] = manifest.SupportEmail;
        if (manifest.Description is not null)
            props["description"] = manifest.Description;
        if (manifest.License is not null)
            props["license"] = manifest.License;
        if (manifest.LicenseUrl is not null)
            props["license-url"] = manifest.LicenseUrl;
        if (manifest.Copyright is not null)
            props["copyright"] = manifest.Copyright;
        if (manifest.Contributors is not null)
            props["contributors"] = manifest.Contributors;
        if (manifest.ProjectWebsite is not null)
            props["project-website"] = manifest.ProjectWebsite;
        if (manifest.DocumentationWebsite is not null)
        {
            props["documentation-website"] = manifest.DocumentationWebsite;
        }
        if (manifest.TermsUrl is not null)
            props["terms-url"] = manifest.TermsUrl;

        foreach (var (key, value) in manifest.Properties)
            props[key] = value;

        var block = new TwBlock("package", manifest.Name, props, []);
        return "# Tinkwell Package Manifest\n" + TwWriter.Write(block);
    }

    private static string? GetString(TwBlock block, string key) =>
        block.Properties.TryGetValue(key, out var v) ? v : null;

    private static bool HasControlCharacters(string value)
    {
        foreach (char c in value)
        {
            if (char.IsControl(c) && c != '\t')
                return true;
        }
        return false;
    }

    private static int GetInt(TwBlock block, string key, int defaultValue)
    {
        if (!block.Properties.TryGetValue(key, out var v))
            return defaultValue;
        if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new PackageException($"Expected integer for '{key}', got '{v}'");
    }
}
