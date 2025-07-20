
namespace Tinkwell.HealthCheck;

interface IProcessInspector
{
    (DateTime Timestamp, TimeSpan ProcessorTime, long AllocatedMemory, int ThreadCount, int HandleCount) Inspect();
}