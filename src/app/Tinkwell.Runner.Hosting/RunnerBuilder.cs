using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Logging;
using Tinkwell.Telemetry;

namespace Tinkwell.Runner.Hosting;

/// <summary>
/// Infrastructure base class for all runners managed by the coordinator.
/// Implements the build phase: parse command-line options, fetch
/// configuration via named pipe, run subclass initialization, build
/// a Generic Host, and return a <see cref="RunnerApp"/> ready to execute.
/// </summary>
/// <remarks>
/// <para>
/// This class is not directly derivable from outside the assembly —
/// its constructor is <see langword="internal"/>. External code should
/// derive from <see cref="StandaloneRunnerBuilder"/> (for runners without
/// runlets) or <see cref="RunnerHostBuilder"/> (for runners that host runlets).
/// </para>
/// <para>
/// Subclasses customize behavior through virtual hooks called at specific
/// points during <see cref="BuildAsync"/>: <see cref="InitializeAsync"/>,
/// <see cref="BuildHost"/>, and <see cref="OnHostBuiltAsync"/>.
/// </para>
/// </remarks>
public abstract class RunnerBuilder
{
    /// <summary>
    /// The original command-line arguments passed to the runner process.
    /// </summary>
    protected string[] Args { get; }

    /// <summary>
    /// The runner's descriptor (ID, name, settings) received from the
    /// coordinator. Available after <see cref="BuildAsync"/> fetches the
    /// configuration but before any virtual hooks are called.
    /// </summary>
    protected RunnerDescriptor Descriptor { get; private set; } = null!;

    /// <summary>
    /// Raw runlet descriptors received from the coordinator. Empty for
    /// standalone runners. <see cref="RunnerHostBuilder"/> uses these to load
    /// and validate runlet assemblies.
    /// </summary>
    protected RunletDescriptor[] RunletDescriptors { get; private set; } = [];

    internal RunnerBuilder(string[] args) => Args = args;

    /// <summary>
    /// Executes the build phase: parses arguments, fetches configuration
    /// from the coordinator, runs subclass initialization, and builds the
    /// Generic Host. Returns a <see cref="RunnerApp"/> ready for
    /// <see cref="RunnerApp.RunAsync"/>.
    /// </summary>
    [RequiresUnreferencedCode("Subclass initialization may use reflection.")]
    public async Task<RunnerApp> BuildAsync()
    {
        using var earlyLoggerFactory = LoggerFactory.Create(b => b.AddTinkwellConsole());
        var logger = earlyLoggerFactory.CreateLogger("Tinkwell.Runner");

        var options = ParseOptions(logger);

        logger.LogDebug(
            "Runner starting (ID: {Id}, pipe: {Pipe})",
            options.RunnerId, options.CoordinatorPipe);

        var client = new CoordinatorPipeClient(options.CoordinatorPipe, logger);

        using (OtTraces.Source.StartActivity(OtTraces.FetchConfig))
            await FetchConfigAsync(client, options.RunnerId, logger);

        logger.LogDebug(
            "Runner '{Name}' (ID: {Id}): received {Count} runlet descriptor(s)",
            Descriptor.Name, Descriptor.Id, RunletDescriptors.Length);

        using (OtTraces.Source.StartActivity(OtTraces.Initialize))
            await InitializeAsync(options, client, logger);

        IHost host;
        using (OtTraces.Source.Timed(OtTraces.BuildHost, OtMetrics.HostBuildDuration))
            host = BuildHost(Args, options, client, Descriptor);

        await OnHostBuiltAsync(host);

        return CreateApp(host, options, client);
    }

    /// <summary>
    /// Convenience method that builds the runner and immediately executes it.
    /// Returns the process exit code.
    /// </summary>
    [RequiresUnreferencedCode("Subclass initialization may use reflection.")]
    public async Task<int> BuildAndRunAsync()
    {
        using var lifecycle = OtTraces.Source.Timed(
            OtTraces.Lifecycle, OtMetrics.LifecycleDuration);

        RunnerApp app;
        try
        {
            app = await BuildAsync();
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            lifecycle.Error(ex.Message);
            Console.Error.WriteLine($"Fatal: runner build failed: {ex}");
            return 1;
        }

        return await app.RunAsync();
    }

    /// <summary>
    /// Called after the configuration has been fetched from the coordinator
    /// but before the host is built. <see cref="Descriptor"/> and
    /// <see cref="RunletDescriptors"/> are available at this point.
    /// Override to perform custom initialization (e.g., loading runlets).
    /// </summary>
    protected virtual Task InitializeAsync(
        RunnerOptions options, CoordinatorPipeClient client, ILogger logger) =>
        Task.CompletedTask;

    /// <summary>
    /// Builds the <see cref="IHost"/> for this runner.
    /// <see cref="StandaloneRunnerBuilder"/> provides a default implementation
    /// that creates a plain Generic Host. <see cref="RunnerHostBuilder"/>
    /// delegates to a runlet-aware overload.
    /// </summary>
    protected abstract IHost BuildHost(
        string[] args, RunnerOptions options,
        CoordinatorPipeClient client, RunnerDescriptor descriptor);

    /// <summary>
    /// Called after the host is built but before it starts.
    /// Override for post-build, pre-start setup.
    /// </summary>
    protected virtual Task OnHostBuiltAsync(IHost host) => Task.CompletedTask;

    /// <summary>
    /// Creates the <see cref="RunnerApp"/> that will execute the run phase.
    /// <see cref="RunnerHostBuilder"/> overrides this to include loaded runlets.
    /// </summary>
    protected virtual RunnerApp CreateApp(
        IHost host, RunnerOptions options, CoordinatorPipeClient client) =>
        new(host, options, client, []);

    /// <summary>
    /// Registers the OpenTelemetry SDK with this runner's trace source and
    /// meter. Call from <see cref="BuildHost"/> overrides that replace the
    /// default host builder.
    /// </summary>
    protected static void AddRunnerTelemetry(
        IServiceCollection services, IConfiguration configuration) =>
        services.AddTinkwellTelemetry(configuration,
            sourceNames: [OtTraces.SourceName, "Tinkwell.Mqtt", "Tinkwell.Coap"],
            meterNames: [OtMetrics.MeterName, "Tinkwell.Mqtt", "Tinkwell.Coap",
                ChannelDropTracker.MeterName]);

    /// <summary>
    /// Registers the <see cref="IServiceDiscovery"/> singleton backed by
    /// the coordinator pipe client. Reads TLS settings from configuration
    /// to determine channel creation behavior. Call from
    /// <see cref="BuildHost"/> overrides that replace the default host builder.
    /// </summary>
    protected static void AddServiceDiscovery(
        IServiceCollection services, CoordinatorPipeClient client, IConfiguration configuration)
    {
        var tlsOptions = new TlsOptions();
        configuration.GetSection("Tls").Bind(tlsOptions);
        services.AddSingleton<IServiceDiscovery>(new ServiceDiscovery(client, tlsOptions));
    }

    /// <summary>
    /// Sends a <c>notify fatal</c> message to the coordinator, swallowing
    /// any communication errors.
    /// </summary>
    protected static async Task NotifyFatalSafe(
        CoordinatorPipeClient client, string runnerId, string message, ILogger? logger = null)
    {
        try { await client.NotifyFatalAsync(runnerId, message); }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            logger?.LogTrace(ex, "Failed to send fatal notification to coordinator");
        }
    }

    private RunnerOptions ParseOptions(ILogger logger)
    {
        try
        {
            return RunnerOptions.Parse(Args);
        }
        catch (ArgumentException ex)
        {
            logger.LogCritical(ex, "Invalid command-line arguments");
            throw;
        }
    }

    private async Task FetchConfigAsync(
        CoordinatorPipeClient client, string runnerId, ILogger logger)
    {
        try
        {
            var (descriptor, runlets) = await client.FetchRunnerConfigAsync(runnerId);
            Descriptor = descriptor;
            RunletDescriptors = runlets;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to fetch configuration from coordinator");
            throw;
        }
    }
}