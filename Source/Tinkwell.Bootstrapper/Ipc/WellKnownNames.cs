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
    /// <strong>Internal</strong>. Use <c>ServiceLocator</c> instead.
    /// <para>
    /// The host address for the Discovery Service.
    /// </para>
    /// </summary>
    public static readonly string DiscoveryServiceAddressEnvironmentVariable = "TINKWELL_DISCOVERY_SERVICE_ADDRESS";

    /// <summary>
    /// <strong>Internal</strong>. Use <c>HostingInformation</c> instead.
    /// An optional environment variable that contains the <em>environment</em> for this
    /// installation. It could be <c>"Development"</c> or <c>"Release"</c>.
    /// <c>TINKWELL_ENVIRONMENT</c>.
    /// </summary>
    public static readonly string EnvironmentEnvironmentVariable = "TINKWELL_ENVIRONMENT";

    /// <summary>
    /// An optional environment variable that contains the directory used to resolve
    /// relative file paths. If not specified then the working directory is used.
    /// It must be an absolute path.
    /// <c>TINKWELL_WORKING_DIR_PATH</c>.
    /// </summary>
    /// <remarks>
    /// <strong>Internal</strong>: do not use this variable directly, obtain the value from
    /// <see cref="Hosting.HostingInformation.WorkingDirectory"/>.
    /// </remarks>
    public static readonly string WorkingDirectoryEnvironmentVariable = "TINKWELL_WORKING_DIR_PATH";

    /// <summary>
    /// An optional environment variable that contains the directory used as data directory
    /// at application level. If not specified it resolves to a directory shared among all users
    /// on the machine (or the user's profile if inaccessible). It must be an absolute path.
    /// <c>TINKWELL_APP_DATA_PATH</c>.
    /// </summary>
    /// <remarks>
    /// <strong>Internal</strong>: do not use this variable directly, obtain the value from
    /// <see cref="Hosting.HostingInformation.ApplicationDataDirectory"/>.
    /// </remarks>
    public static readonly string AppDataEnvironmentVariable = "TINKWELL_APP_DATA_PATH";

    /// <summary>
    /// An optional environment variable that contains the directory used as data directory
    /// at user level. If not specified it resolves to a directory in the user's profile.
    /// It must be an absolute path.
    /// <c>TINKWELL_USER_DATA_PATH</c>.
    /// </summary>
    /// <remarks>
    /// <strong>Internal</strong>: do not use this variable directly, obtain the value from
    /// <see cref="Hosting.HostingInformation.UserDataDirectory"/>.
    /// </remarks>
    public static readonly string UserDataEnvironmentVariable = "TINKWELL_USER_DATA_PATH";

    /// <summary>
    /// <strong>Caution</strong>. If possible try to use <c>ServiceLocator</c>.
    /// <para>
    /// The environment variable for the client certificate for gRPC HTTPS calls.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly string ClientCertificatePath = "TINKWELL_CLIENT_CERT_PATH";

    /// <summary>
    /// <strong>Caution</strong>. If possible try to use an existing host.
    /// <para>
    /// The environment variable for the web server certificate path.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly string WebServerCertificatePath = "TINKWELL_CERT_PATH";

    /// <summary>
    /// <strong>Caution</strong>. If possible try to use an existing host.
    /// <para>
    /// The environment variable for the web server certificate password.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly string WebServerCertificatePass = "TINKWELL_CERT_PASS";

    /// <summary>
    /// <strong>Internal, do not use</strong>.
    /// <para>
    /// The default assembly name for the gRPC host.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly string DefaultGrpcHostAssembly = "Tinkwell.Bootstrapper.GrpcHost";

    /// <summary>
    /// <strong>Internal, do not use</strong>.
    /// <para>
    /// The default assembly name for the DLL host.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly string DefaultDllHostAssembly = "Tinkwell.Bootstrapper.DllHost";

    /// <summary>
    /// <strong>Internal, do not use</strong>.
    /// <para>
    /// The default health check service DLL name.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly string DefaultHealthCheckService = "Tinkwell.HealthCheck.dll";

    /// <summary>
    /// The event topic name for signals.
    /// </summary>
    public static readonly string EventTopicSignal = "signal";

    /// <summary>
    /// The event topic name for malfunctions.
    /// </summary>
    public static readonly string EventTopicMalfunction = "malfunction";
}
