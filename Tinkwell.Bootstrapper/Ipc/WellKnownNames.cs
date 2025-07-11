namespace Tinkwell.Bootstrapper.Ipc;

/// <summary>
/// Provides well-known names and environment variable keys used throughout Tinkwell Bootstrapper IPC.
/// </summary>
public static class WellKnownNames
{
    /// <summary>
    /// The role name for the discovery service.
    /// </summary>
    public static readonly string DiscoveryServiceRoleName = "discovery";
    /// <summary>
    /// The pipe name for the supervisor command server.
    /// </summary>
    public static readonly string SupervisorCommandServerPipeName = "tinkwell-supervisor-command-server";

    /// <summary>
    /// The environment variable for the runner name.
    /// </summary>
    public static readonly string RunnerNameEnvironmentVariable = "TINKWELL_RUNNER_NAME";
    /// <summary>
    /// The environment variable for the supervisor PID.
    /// </summary>
    public static readonly string SupervisorPidEnvironmentVariable = "TINKWELL_SUPERVISOR_PID";

    /// <summary>
    /// The environment variable for the web server certificate path.
    /// </summary>
    public static readonly string WebServerCertificatePath = "TINKWELL_CERT_PATH";
    /// <summary>
    /// The environment variable for the web server certificate password.
    /// </summary>
    public static readonly string WebServerCertificatePass = "TINKWELL_CERT_PASS";

    /// <summary>
    /// The default assembly name for the gRPC host.
    /// </summary>
    public static readonly string DefaultGrpcHostAssembly = "Tinkwell.Bootstrapper.GrpcHost";
    /// <summary>
    /// The default assembly name for the DLL host.
    /// </summary>
    public static readonly string DefaultDllHostAssembly = "Tinkwell.Bootstrapper.DllHost";
    /// <summary>
    /// The default health check service DLL name.
    /// </summary>
    public static readonly string DefaaultHealthCheckService = "Tinkwell.HealthCheck.dll";

    /// <summary>
    /// The event topic name for signals.
    /// </summary>
    public static readonly string EventTopicSignal = "signal";
    /// <summary>
    /// The event topic name for malfunctions.
    /// </summary>
    public static readonly string EventTopicMalfunction = "malfunction";
}
