using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Supervisor;

interface IChildProcess
{
    public int Id { get; }

    string? Host { get; }

    RunnerDefinition Definition { get; }

    void Start();

    void Stop();

    void Restart();
}
