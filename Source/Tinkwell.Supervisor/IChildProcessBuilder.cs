using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Supervisor;

interface IChildProcessBuilder
{
    string? DiscoveryServiceAddress { get; set; }

    IChildProcess Create(RunnerDefinition definition);
}
