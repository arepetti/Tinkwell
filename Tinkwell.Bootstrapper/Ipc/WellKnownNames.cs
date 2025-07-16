using System.ComponentModel;

namespace Tinkwell.Bootstrapper.Ipc;

/// <summary>
/// Provides well-known names used throughout Tinkwell for bootstrapping and co-ordination.
/// Most of the names are <strong>internal and should not be used</strong> directly in production code.
/// </summary>
public static class WellKnownNames
{
    /// <summary>
    /// <strong>Internal</strong>. Use <c>ServiceLocator</c> instead.
    /// <para>
    /// The role name for the discovery service.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly string DiscoveryServiceRoleName = "discovery";

    /// <summary>
    /// <strong>Internal</strong>. Use <c>HostingInformation</c> or <c>OrchestratorService</c> instead.
    /// <para>
    /// The pipe name for the supervisor command server.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly string SupervisorCommandServerPipeName = "tinkwell-supervisor-command-server";

    /// <summary>
    /// <strong>Internal</strong>. Use <c>HostingInformation</c> instead.
    /// <para>
    /// The environment variable for the runner name.
    /// </para>
    /// </summary>
    public static readonly string RunnerNameEnvironmentVariable = "TINKWELL_RUNNER_NAME";

    /// <summary>
    /// <strong>Internal</strong>. Use <c>ParentProcessWatcher</c> instead.
    /// <para>
    /// The environment variable for the supervisor PID.
    /// </para>
    /// </summary>
    public static readonly string SupervisorPidEnvironmentVariable = "TINKWELL_SUPERVISOR_PID";

    /// <summary>
    /// An optional environment variable that contains the <em>environment</em> for this
    /// installation. It could be <c>"Development"</c> or <c>"Release"</c>.
    /// </summary>
    public static readonly string EnvironmentEnvironmentVariable = "TINKWELL_ENVIRONMENT";

    /// <summary>
    /// An optional environment variable that contains the directory used to resolve
    /// relative file paths. If not specified then the working directory is used.
    /// </summary>
    public static readonly string WorkingDirectoryEnvironmentVariable = "TINKWELL_APP_DATA_PATH";

    /// <summary>
    /// <strong>Internal</strong>. If possible try to use an existing host.
    /// <para>
    /// The environment variable for the web server certificate path.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly string WebServerCertificatePath = "TINKWELL_CERT_PATH";

    /// <summary>
    /// <strong>Internal</strong>. If possible try to use an existing host.
    /// <para>
    /// The environment variable for the web server certificate password.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly string WebServerCertificatePass = "TINKWELL_CERT_PASS";

    /// <summary>
    /// <strong>Internal, do not use.</strong>.
    /// <para>
    /// The default assembly name for the gRPC host.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly string DefaultGrpcHostAssembly = "Tinkwell.Bootstrapper.GrpcHost";

    /// <summary>
    /// <strong>Internal, do not use.</strong>.
    /// <para>
    /// The default assembly name for the DLL host.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly string DefaultDllHostAssembly = "Tinkwell.Bootstrapper.DllHost";

    /// <summary>
    /// <strong>Internal, do not use.</strong>.
    /// <para>
    /// The default health check service DLL name.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
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
