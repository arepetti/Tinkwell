using System.Text;

namespace Tinkwell.Bootstrapper.IO;

public sealed class PhysicalFileSytem : IFileSystem
{
    public string? GetDirectoryName(string path)
        => Path.GetDirectoryName(path);

    public string TryCombineIfRelative(string basePath, string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        return Path.Combine(basePath, path);
    }

    public string ResolveFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Directory.GetCurrentDirectory();

        if (Path.IsPathFullyQualified(path))
            return path;

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
    }

    public string ResolveExecutableWorkingDirectory(string path)
    {
        if (Path.IsPathFullyQualified(path))
            return Path.GetDirectoryName(path)!;

        return Directory.GetCurrentDirectory();
    }

    public string GetCurrentDirectory()
        => Directory.GetCurrentDirectory();

    public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken)
        => new(File.Exists(path));

    public Task<string> ReadAllTextAsync(string path, Encoding encoding, CancellationToken cancellationToken)
        => File.ReadAllTextAsync(path, encoding, cancellationToken);
}