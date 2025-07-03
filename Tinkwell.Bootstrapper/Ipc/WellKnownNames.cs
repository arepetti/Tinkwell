namespace Tinkwell.Bootstrapper.Ipc;

public static class WellKnownNames
{
    public static readonly string DiscoveryServiceRoleName = "discovery";
    public static readonly string SupervisorCommandServerPipeName = "tinkwell-supervisor-command-server";

    public static readonly string RunnerNameEnvironmentVariable = "TINKWELL_RUNNER_NAME";
    public static readonly string SupervisorPidEnvironmentVariable = "TINKWELL_SUPERVISOR_PID";

    public static readonly string DefaultGrpcHostAssembly = "Tinkwell.Bootstrapper.GrpcHost";
    public static readonly string DefaultDllHostAssembly = "Tinkwell.Bootstrapper.DllHost";
    public static readonly string DefaaultHealthCheckService = "Tinkwell.HealthCheck.dll";


    public static readonly string EventTopicSignal = "signal";
    public static readonly string EventTopicMalfunction = "malfunction";
}
