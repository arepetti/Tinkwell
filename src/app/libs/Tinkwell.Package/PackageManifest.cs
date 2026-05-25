namespace Tinkwell.Package;

/// <summary>
/// Parsed content of a <c>package.tw</c> manifest.
/// </summary>
public sealed record PackageManifest
{
    /// <summary>Package identifier (the block name in <c>package.tw</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Manifest <c>format-version</c> field. Default: <c>1</c>.</summary>
    public int FormatVersion { get; init; } = 1;

    /// <summary>Optional content type (e.g. <c>plugin</c>).</summary>
    public string? Type { get; init; }

    /// <summary>Package version (often semver), if present in the manifest.</summary>
    public string? Version { get; init; }

    /// <summary>Primary author name from the manifest.</summary>
    public string? Author { get; init; }

    /// <summary>Author contact email.</summary>
    public string? AuthorEmail { get; init; }

    /// <summary>Company or organization name.</summary>
    public string? Company { get; init; }

    /// <summary>Company website URL.</summary>
    public string? CompanyWebsite { get; init; }

    /// <summary>Company contact email.</summary>
    public string? CompanyEmail { get; init; }

    /// <summary>Support or help-desk email.</summary>
    public string? SupportEmail { get; init; }

    /// <summary>Short package description.</summary>
    public string? Description { get; init; }

    /// <summary>License identifier (e.g. MIT, Apache-2.0).</summary>
    public string? License { get; init; }

    /// <summary>URL to the full license text.</summary>
    public string? LicenseUrl { get; init; }

    /// <summary>Copyright notice.</summary>
    public string? Copyright { get; init; }

    /// <summary>Contributor names, as stored in the manifest (often comma-separated).</summary>
    public string? Contributors { get; init; }

    /// <summary>Project homepage URL.</summary>
    public string? ProjectWebsite { get; init; }

    /// <summary>Documentation site URL.</summary>
    public string? DocumentationWebsite { get; init; }

    /// <summary>URL to terms and conditions.</summary>
    public string? TermsUrl { get; init; }

    /// <summary>
    /// Optional plugin subtype for specialized discovery (e.g.
    /// <c>"Tinkwell.StateMachines.CompilerBackend"</c>). When set, consumers may
    /// exclude the package from general plugin resolution in favor of a specific toolchain.
    /// </summary>
    public string? Subtype { get; init; }

    /// <summary>
    /// All properties not recognized as well-known fields, passed through as-is.
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } =
        new Dictionary<string, string>();
}
