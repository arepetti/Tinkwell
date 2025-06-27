using System.Diagnostics;

namespace Tinkwell.HealthCheck;

sealed class CurrentProcessInspector : IProcessInspector
{
    public (DateTime Timestamp, TimeSpan ProcessorTime, long AllocatedMemory, int ThreadCount, int HandleCount) Inspect()
    {
        if (_process is null)
            _process = Process.GetCurrentProcess();
        else
            _process.Refresh();

        return (DateTime.UtcNow, _process.TotalProcessorTime, _process.WorkingSet64, _process.Threads.Count, _process.HandleCount);
    }

    private Process? _process;
}
