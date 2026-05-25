namespace Tinkwell.Package;

/// <summary>
/// Default implementation of <see cref="IPackageValidator"/> with all
/// standard security checks.
/// </summary>
public sealed class PackageValidator : IPackageValidator
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly char[] ForbiddenChars = [':', '\0'];

    /// <inheritdoc />
    public string NormalizeEntryPath(string rawPath)
    {
        var normalized = rawPath.Replace('\\', '/');
        return normalized.TrimStart('/');
    }

    /// <inheritdoc />
    public void ValidateEntryPath(string normalizedPath, int maxPathLength)
    {
        if (string.IsNullOrWhiteSpace(normalizedPath))
            throw new PackageException("Empty entry path");

        if (normalizedPath.Length > maxPathLength)
            throw new PackageException(
                $"Path exceeds maximum length ({normalizedPath.Length} > {maxPathLength}): {normalizedPath}");

        if (Path.IsPathRooted(normalizedPath))
            throw new PackageException($"Absolute path not allowed: {normalizedPath}");

        foreach (var ch in ForbiddenChars)
        {
            if (normalizedPath.Contains(ch))
                throw new PackageException(
                    $"Path contains forbidden character '{(ch == '\0' ? "\\0" : ch.ToString())}': {normalizedPath}");
        }

        var segments = normalizedPath.Split('/');
        foreach (var segment in segments)
        {
            if (segment == "..")
                throw new PackageException($"Path traversal detected: {normalizedPath}");

            if (segment == ".")
                throw new PackageException($"Self-referencing path segment: {normalizedPath}");

            if (segment.Length == 0)
                continue; // trailing slash, harmless

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(segment);
            if (ReservedNames.Contains(nameWithoutExtension))
                throw new PackageException(
                    $"Reserved file name '{segment}' in path: {normalizedPath}");
        }

        var fullPath = Path.GetFullPath(Path.Combine("root", normalizedPath));
        var rootFull = Path.GetFullPath("root") + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new PackageException($"Path escapes package root: {normalizedPath}");
    }

    /// <inheritdoc />
    public void ValidateFileSize(string path, long size, long maxFileSize)
    {
        if (size > maxFileSize)
            throw new PackageException(
                $"File size {size} exceeds maximum {maxFileSize}: {path}");
    }

    /// <inheritdoc />
    public void ValidateTotalSize(long totalSize, long maxTotalSize)
    {
        if (totalSize > maxTotalSize)
            throw new PackageException(
                $"Total decompressed size {totalSize} exceeds maximum {maxTotalSize}");
    }

    /// <inheritdoc />
    public void AccumulateDecompressedSize(
        ref long totalSize, long entryUncompressedSize, long maxDecompressed)
    {
        if (entryUncompressedSize > long.MaxValue - totalSize)
            throw new PackageException(
                $"Total decompressed size exceeds maximum ({maxDecompressed})");

        totalSize += entryUncompressedSize;
        ValidateTotalSize(totalSize, maxDecompressed);
    }

    /// <inheritdoc />
    public void ValidateFileCount(int count, int maxCount)
    {
        if (count > maxCount)
            throw new PackageException(
                $"Entry count {count} exceeds maximum {maxCount}");
    }

    /// <inheritdoc />
    public void ValidatePackageStructure(IReadOnlySet<string> entryPaths)
    {
        if (!entryPaths.Contains(WellKnownPaths.Manifest))
            throw new PackageException(
                $"Package is missing required '{WellKnownPaths.Manifest}'");

        var allowedPrefixes = new[]
        {
            WellKnownPaths.Manifest,
            WellKnownPaths.ContentDirectory + "/",
            WellKnownPaths.SecurityDirectory + "/",
        };

        foreach (var path in entryPaths)
        {
            if (!allowedPrefixes.Any(prefix =>
                path.Equals(prefix, StringComparison.Ordinal) ||
                path.StartsWith(prefix, StringComparison.Ordinal)))
            {
                throw new PackageException(
                    $"File outside allowed structure: {path}");
            }
        }
    }

    /// <inheritdoc />
    public void ValidateCompleteness(
        IReadOnlySet<string> signedPaths, IReadOnlySet<string> actualPaths)
    {
        var verifiablePaths = actualPaths
            .Where(p => !p.StartsWith(WellKnownPaths.SecurityDirectory + "/", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var signed in signedPaths)
        {
            if (!verifiablePaths.Contains(signed))
                throw new PackageException($"Signed file not found in package: {signed}");
        }

        foreach (var actual in verifiablePaths)
        {
            if (!signedPaths.Contains(actual))
                throw new PackageException($"Undeclared file in package: {actual}");
        }
    }

    /// <inheritdoc />
    public void ValidateSignatureOrder(IReadOnlyList<string> paths)
    {
        for (int i=1; i < paths.Count; ++i)
        {
            if (string.Compare(paths[i - 1], paths[i], StringComparison.Ordinal) >= 0)
                throw new PackageException(
                    $"Signatures not in canonical order: '{paths[i - 1]}' before '{paths[i]}'");
        }
    }
}
