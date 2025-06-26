using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Supervisor.Commands;

namespace Tinkwell.Supervisor;

interface IRegistry
{
    IEnumerable<IChildProcess> Items { get; }

    Task StartAsync(ICommandServer commandServer, string configurationPath, CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    IChildProcess? FindByName(string name);

    IChildProcess? FindById(int pid);

    IEnumerable<IChildProcess> FindAllByQuery(string? query);

    void AddNew(RunnerDefinition definition, bool start);

    void AddNew(string name, string path, string arguments, bool start = true)
        => AddNew(new RunnerDefinition { Name = name, Path = path, Arguments = arguments }, start);
}
