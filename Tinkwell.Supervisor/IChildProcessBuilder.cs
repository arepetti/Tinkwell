using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Supervisor;

interface IChildProcessBuilder
{
    IChildProcess Create(RunnerDefinition definition);
}
