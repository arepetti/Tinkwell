using System.Diagnostics;

namespace Tinkwell.Watchdog;

[DebuggerDisplay("{Runner} (#{Pid})")]
record Runner(string Name, int Pid, RunnerRole Role);
