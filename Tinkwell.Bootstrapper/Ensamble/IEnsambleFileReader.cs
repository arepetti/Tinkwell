
namespace Tinkwell.Bootstrapper.Ensamble;

public interface IEnsambleFileReader
{
    Task<IEnumerable<RunnerDefinition>> ReadAsync(string path, CancellationToken cancellationToken);
}