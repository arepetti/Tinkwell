using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Health;
using Tinkwell.Logging;
using Tinkwell.Telemetry;

namespace Tinkwell.Runner.Hosting;

/// <summary>
/// Abstract builder for runner containers that host runlets. Extends
/// <see cref="RunnerBuilder"/> with runlet-specific lifecycle: loading
/// assemblies, validating interface compatibility, registering services,
/// and mapping endpoints.
/// </summary>
/// <remarks>
/// <para>
/// Subclasses override the runlet-aware virtual methods to add
/// container-specific setup. For example, a gRPC runner overrides
/// <see cref="BuildHost(string[], RunnerOptions, CoordinatorPipeClient, IReadOnlyList{RunletState}, RunnerDescriptor)"/>
/// to use <c>WebApplication.CreateBuilder</c>, <see cref="ValidateRunlet"/>
/// to require <c>IGrpcRunlet</c>, and <see cref="ConfigureRunlet"/> to
/// register gRPC services.
/// </para>
/// <para>
/// The headless runner needs no overrides — the base implementation
/// accepts any <see cref="IRunlet"/> and builds a plain Generic Host.
/// </para>
/// <para>
/// For runners that do not host runlets, derive from
/// <see cref="StandaloneRunnerBuilder"/> instead.
/// </para>
/// </remarks>
public abstract class RunnerHostBuilder : RunnerBuilder
{
    private List<RunletState>? _loadedRunlets;
    private PluginResolver? _pluginResolver;

    protected RunnerHostBuilder(string[] args) : base(args) { }

    /// <summary>
    /// Validates that a loaded runlet is compatible with this runner type.
    /// Override to enforce transport-specific interface requirements
    /// (e.g., require <c>IWebRunlet</c>). Throw to reject the runlet.
    /// The default implementation accepts any <see cref="IRunlet"/>.
    /// </summary>
    protected virtual void ValidateRunlet(RunletState runlet) { }

    /// <summary>
    /// Configures the host builder before runlet services are registered.
    /// Override to add container-specific services (e.g., Kestrel, gRPC server).
    /// </summary>
    protected virtual void ConfigureHost(HostApplicationBuilder builder, IReadOnlyList<RunletState> runlets) { }

    /// <summary>
    /// Registers a single runlet's services into the DI container.
    /// The default implementation calls <see cref="IRunlet.ConfigureServices"/>.
    /// Override for transport-specific registration (e.g., calling
    /// <c>IGrpcRunlet.MapGrpcServices</c> alongside the base registration).
    /// </summary>
    protected virtual void ConfigureRunlet(IServiceCollection services, RunletState runlet, IConfiguration settings)
    {
        runlet.Instance.ConfigureServices(services, settings);
    }

    /// <summary>
    /// Builds the <see cref="IHost"/> for this runner with runlet awareness.
    /// The default implementation creates a plain Generic Host, registers
    /// core singletons, calls <see cref="ConfigureHost"/>, and iterates
    /// runlets calling <see cref="ConfigureRunlet"/> for each.
    /// Override to use a different builder (e.g., <c>WebApplication.CreateBuilder</c>).
    /// </summary>
    protected virtual IHost BuildHost(
        string[] args, RunnerOptions options,
        CoordinatorPipeClient client, IReadOnlyList<RunletState> loaded,
        RunnerDescriptor descriptor)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.AddTinkwellConsole();

        builder.Services.AddSingleton(descriptor);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(client);
        if (_pluginResolver is not null)
            builder.Services.AddSingleton(_pluginResolver);
        AddRunnerTelemetry(builder.Services, builder.Configuration);
        AddServiceDiscovery(builder.Services, client, builder.Configuration);

        AddHealthServices(builder.Services, descriptor);
        ConfigureHost(builder, loaded);

        foreach (var runlet in loaded)
        {
            var settings = new ConfigurationBuilder()
                .AddInMemoryCollection(runlet.Descriptor.Settings ?? new Dictionary<string, string>())
                .Build();

            ConfigureRunlet(builder.Services, runlet, settings);
        }

        return builder.Build();
    }

    /// <summary>
    /// Called after the host is built but before it starts.
    /// Override for post-build, pre-start setup with access to loaded runlets.
    /// </summary>
    protected virtual Task OnHostBuiltAsync(IHost host, IReadOnlyList<RunletState> runlets) =>
        Task.CompletedTask;

    /// <summary>
    /// Called after runlets are loaded and validated but before the host is
    /// built. Override to perform async setup that depends on runlet state
    /// (e.g., requesting a network endpoint from the coordinator).
    /// </summary>
    protected virtual Task OnRunletsLoadedAsync(
        RunnerOptions options, CoordinatorPipeClient client, ILogger logger) =>
        Task.CompletedTask;

    [RequiresUnreferencedCode("Runlet loading uses reflection.")]
    protected sealed override async Task InitializeAsync(
        RunnerOptions options, CoordinatorPipeClient client, ILogger logger)
    {
        using var activity = OtTraces.Source.StartActivity(OtTraces.LoadRunlets);

        var catalog = new PluginCatalog(logger);
        if (catalog.Plugins.Count > 0)
        {
            _pluginResolver = new PluginResolver(catalog, logger);
            logger.LogInformation("Plugin catalog: {Count} plugin(s) discovered", catalog.Plugins.Count);
        }

        logger.LogDebug("Loading {Count} runlet(s)", RunletDescriptors.Length);

        List<RunletState> loaded;
        try
        {
            loaded = RunletLoader.LoadAll(RunletDescriptors, logger, _pluginResolver);
            OtMetrics.RunletsLoaded.Add(loaded.Count);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            activity?.Error(ex.Message);
            logger.LogCritical(ex, "Failed to load runlets");
            await NotifyFatalSafe(client, options.RunnerId, $"Runlet load failed: {ex.Message}", logger);
            throw;
        }

        try
        {
            foreach (var runlet in loaded)
            {
                using var validateActivity = OtTraces.Source.Start(OtTraces.ValidateRunlet,
                    (OtTraces.RunletName, runlet.Descriptor.Name), (OtTraces.RunletAssembly, runlet.Descriptor.AssemblyPath));
                ValidateRunlet(runlet);
            }
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            activity?.Error(ex.Message);
            logger.LogCritical(ex, "Runlet validation failed");
            await NotifyFatalSafe(client, options.RunnerId, ex.Message, logger);
            throw;
        }

        _loadedRunlets = loaded;

        await OnRunletsLoadedAsync(options, client, logger);
    }

    protected sealed override IHost BuildHost(
        string[] args, RunnerOptions options,
        CoordinatorPipeClient client, RunnerDescriptor descriptor) =>
        BuildHost(args, options, client, _loadedRunlets!, descriptor);

    protected sealed override Task OnHostBuiltAsync(IHost host) =>
        OnHostBuiltAsync(host, _loadedRunlets!);

    protected sealed override RunnerApp CreateApp(
        IHost host, RunnerOptions options, CoordinatorPipeClient client) =>
        new(host, options, client, _loadedRunlets!);

    protected static void AddHealthServices(IServiceCollection services, RunnerDescriptor descriptor)
    {
        var options = new HealthMonitorOptions();
        services.AddSingleton(options);
        services.AddSingleton<ProcessInspector>();
        services.AddSingleton<IHealthReportWriter, StoreHealthReportWriter>();
        services.AddHostedService(sp => new HealthMonitorWorker(
            descriptor.Name,
            sp.GetRequiredService<HealthMonitorOptions>(),
            sp.GetRequiredService<ProcessInspector>(),
            sp.GetServices<IHealthCheck>(),
            sp.GetService<IHealthReportWriter>(),
            sp.GetRequiredService<ILogger<HealthMonitorWorker>>()));
    }
}