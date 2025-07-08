namespace Tinkwell.Bootstrapper.Ensamble;

public interface IConfigFileReader<T>
{
    Task<T> ReadAsync(string path, FileReaderOptions options, CancellationToken cancellationToken);

    Task<T> ReadAsync(string path, CancellationToken cancellationToken)
        => ReadAsync(path, new FileReaderOptions(false), cancellationToken);
}
