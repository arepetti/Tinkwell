namespace Tinkwell.Bootstrapper.Ipc;

public static class WellKnownNames
{
    public static readonly string DiscoveryServiceRoleName = "tinkwell-discovery-service";
    public static readonly string SupervisorCommandServerPipeName = "tinkwell-supervisor-command-server";
    public static readonly string RunnerNameEnvironmentVariable = "TINKWELL_RUNNER_NAME";
    public static readonly string SupervisorPidEnvironmentVariable = "TINKWELL_SUPERVISOR_PID";
}
