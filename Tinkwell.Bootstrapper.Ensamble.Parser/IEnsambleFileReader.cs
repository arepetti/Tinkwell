
namespace Tinkwell.Bootstrapper.Ensamble;

public interface IEnsambleFileReader
{
    Task<IEnsambleFile> ReadAsync(string path, EnsambleFileReadOptions options, CancellationToken cancellationToken);

    Task<IEnsambleFile> ReadAsync(string path, CancellationToken cancellationToken)
        => ReadAsync(path, new EnsambleFileReadOptions(false), cancellationToken);
}
