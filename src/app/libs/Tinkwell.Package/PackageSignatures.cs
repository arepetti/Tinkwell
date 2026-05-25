namespace Tinkwell.Package;

/// <summary>
/// Parsed content of a <c>security/signatures.tw</c> file.
/// </summary>
public sealed record PackageSignatures
{
    /// <summary>Hash algorithm label for signed file entries (e.g. <c>sha512</c>).</summary>
    public required string Algorithm { get; init; }

    /// <summary>All signed file paths with expected hash and size.</summary>
    public required IReadOnlyList<FileSignature> Files { get; init; }
}

/// <summary>
/// Hash and size of a single file within the package.
/// </summary>
public sealed record FileSignature
{
    /// <summary>Path within the package using forward slashes (e.g. <c>content/foo.dll</c>).</summary>
    public required string Path { get; init; }

    /// <summary>Lowercase hex SHA-512 digest of the file bytes.</summary>
    public required string Hash { get; init; }

    /// <summary>Uncompressed byte length of the file.</summary>
    public required long Size { get; init; }
}
