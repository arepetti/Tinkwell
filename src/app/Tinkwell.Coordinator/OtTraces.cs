using System.Diagnostics;

namespace Tinkwell.Coordinator;

internal static class OtTraces
{
    public const string SourceName = "Tinkwell.Coordinator";
    public static readonly ActivitySource Source = new(SourceName);

    public const string Start = "coordinator.start";
    public const string RunnerLaunch = "coordinator.runner.launch";
    public const string RunnerWaitReady = "coordinator.runner.wait_ready";
    public const string ProcessLaunch = "coordinator.process.launch";
    public const string RunnerRestart = "coordinator.runner.restart";
    public const string CommandDispatch = "coordinator.command.dispatch";

    public const string RunnerName = "runner.name";
    public const string RunnerId = "runner.id";
    public const string Command = "pipe.command";
    public const string ProcessPid = "process.pid";
    public const string Result = "result";
}
