using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Supervisor;

interface IRegistry
{
    IEnumerable<IChildProcess> Items { get; }

    Task StartAsync(string configurationPath, CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    IChildProcess? FindByName(string name);

    IChildProcess? FindById(int pid);

    void AddNew(RunnerDefinition definition, bool start);

    void AddNew(string name, string path, string arguments, bool start = true)
        => AddNew(new RunnerDefinition { Name = name, Path = path, Arguments = arguments }, start);
}
