using System.Text;

namespace Tinkwell.Bootstrapper.IO;

public interface IFileSystem
{
    string? GetDirectoryName(string path);

    string TryCombineIfRelative(string basePath, string path);

    string ResolveFullPath(string? path);
    
    string ResolveExecutableWorkingDirectory(string path);

    string GetCurrentDirectory();

    ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken);

    Task<string> ReadAllTextAsync(string path, Encoding encoding, CancellationToken cancellationToken);

    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
        => ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
}
