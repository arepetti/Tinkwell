namespace Tinkwell.Package;

/// <summary>
/// A file to include in a package, with its path relative to the
/// <c>content/</c> directory and a readable stream.
/// </summary>
public sealed record PackageEntry(string RelativePath, Stream Content);
