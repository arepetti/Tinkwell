namespace Tinkwell.Package;

/// <summary>
/// Injectable service for package validation checks. Validation methods
/// throw <see cref="PackageException"/> on constraint violations. Replace
/// the default <see cref="PackageValidator"/> to customize rules.
/// </summary>
public interface IPackageValidator
{
    /// <summary>
    /// Normalizes a raw zip entry path (backslash to forward slash, trim leading slash).
    /// </summary>
    /// <param name="rawPath">The raw path as stored in the zip entry.</param>
    /// <returns>A forward-slash-separated path with no leading slash.</returns>
    string NormalizeEntryPath(string rawPath);

    /// <summary>
    /// Validates a single entry path for traversal attacks, reserved names,
    /// forbidden characters, and length limits.
    /// </summary>
    /// <param name="normalizedPath">A path already processed by <see cref="NormalizeEntryPath"/>.</param>
    /// <param name="maxPathLength">Maximum allowed path length in characters.</param>
    /// <exception cref="PackageException">The path is invalid.</exception>
    void ValidateEntryPath(string normalizedPath, int maxPathLength);

    /// <summary>Validates that a single file does not exceed the size limit.</summary>
    /// <param name="path">Entry path (for diagnostics).</param>
    /// <param name="size">Uncompressed size of the entry in bytes.</param>
    /// <param name="maxFileSize">Maximum allowed size in bytes.</param>
    /// <exception cref="PackageException"><paramref name="size"/> exceeds <paramref name="maxFileSize"/>.</exception>
    void ValidateFileSize(string path, long size, long maxFileSize);

    /// <summary>Validates that the total decompressed size is within limits.</summary>
    /// <param name="totalSize">Running total of decompressed bytes so far.</param>
    /// <param name="maxTotalSize">Maximum allowed total in bytes.</param>
    /// <exception cref="PackageException"><paramref name="totalSize"/> exceeds <paramref name="maxTotalSize"/>.</exception>
    void ValidateTotalSize(long totalSize, long maxTotalSize);

    /// <summary>
    /// Adds <paramref name="entryUncompressedSize"/> to the running decompressed
    /// total, throwing <see cref="PackageException"/> if the sum would overflow
    /// <see cref="long"/> or exceed <paramref name="maxDecompressed"/>.
    /// The default implementation delegates to <see cref="ValidateTotalSize"/>.
    /// </summary>
    /// <param name="totalSize">Running total, updated in place on success.</param>
    /// <param name="entryUncompressedSize">Size of the current entry in bytes.</param>
    /// <param name="maxDecompressed">Maximum allowed total in bytes.</param>
    /// <exception cref="PackageException">The accumulated size would overflow or exceed the limit.</exception>
    void AccumulateDecompressedSize(
        ref long totalSize, long entryUncompressedSize, long maxDecompressed)
    {
        if (entryUncompressedSize > long.MaxValue - totalSize)
            throw new PackageException(
                $"Total decompressed size exceeds maximum ({maxDecompressed})");

        totalSize += entryUncompressedSize;
        ValidateTotalSize(totalSize, maxDecompressed);
    }

    /// <summary>Validates that the number of entries is within limits.</summary>
    /// <param name="count">Number of entries in the archive.</param>
    /// <param name="maxCount">Maximum allowed entry count.</param>
    /// <exception cref="PackageException"><paramref name="count"/> exceeds <paramref name="maxCount"/>.</exception>
    void ValidateFileCount(int count, int maxCount);

    /// <summary>
    /// Validates the top-level package structure: <c>package.tw</c> must exist,
    /// no files outside the three allowed top-level locations
    /// (<c>package.tw</c>, <c>content/</c>, <c>security/</c>).
    /// </summary>
    /// <param name="entryPaths">Normalized paths of every entry in the package.</param>
    /// <exception cref="PackageException">The structure is invalid.</exception>
    void ValidatePackageStructure(IReadOnlySet<string> entryPaths);

    /// <summary>
    /// Validates bidirectional completeness: every signed path must exist on disk,
    /// and every actual path (outside <c>security/</c>) must appear in the signed set.
    /// </summary>
    /// <param name="signedPaths">Paths listed in <c>signatures.tw</c>.</param>
    /// <param name="actualPaths">Paths found on disk (including <c>security/</c> entries).</param>
    /// <exception cref="PackageException">A file is missing or undeclared.</exception>
    void ValidateCompleteness(
        IReadOnlySet<string> signedPaths, IReadOnlySet<string> actualPaths);

    /// <summary>
    /// Validates that signature entries are in strict ascending ordinal order.
    /// </summary>
    /// <param name="paths">The file paths from <c>signatures.tw</c>, in the order they appear.</param>
    /// <exception cref="PackageException">Paths are not in canonical order.</exception>
    void ValidateSignatureOrder(IReadOnlyList<string> paths);
}
