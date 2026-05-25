using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Logging;

namespace Tinkwell.Runner.Hosting;

/// <summary>
/// Base builder for runners that operate without hosting runlets. Provides
/// the full coordinator lifecycle (config fetch, sentinel monitoring,
/// ready notification) out of the box. The default <see cref="BuildHost"/>
/// creates a plain Generic Host with core singletons registered.
/// Override <see cref="RunnerBuilder.InitializeAsync"/>,
/// <see cref="ConfigureHost"/>, or <see cref="RunnerBuilder.OnHostBuiltAsync"/>
/// to customize behavior.
/// </summary>
/// <remarks>
/// For runners that load and host runlets, derive from
/// <see cref="RunnerHostBuilder"/> instead.
/// </remarks>
public abstract class StandaloneRunnerBuilder : RunnerBuilder
{
    protected StandaloneRunnerBuilder(string[] args) : base(args) { }

    /// <summary>
    /// Configures the host builder before it is finalized.
    /// Override to add runner-specific services.
    /// </summary>
    protected virtual void ConfigureHost(HostApplicationBuilder builder) { }

    /// <summary>
    /// Builds a plain Generic Host and registers <see cref="RunnerBuilder.Descriptor"/>,
    /// <see cref="RunnerOptions"/>, and <see cref="CoordinatorPipeClient"/>
    /// as singletons. Override <see cref="ConfigureHost"/> to add custom services.
    /// </summary>
    protected override IHost BuildHost(
        string[] args, RunnerOptions options,
        CoordinatorPipeClient client, RunnerDescriptor descriptor)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.AddTinkwellConsole();

        builder.Services.AddSingleton(descriptor);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(client);
        AddRunnerTelemetry(builder.Services, builder.Configuration);
        AddServiceDiscovery(builder.Services, client, builder.Configuration);

        ConfigureHost(builder);

        return builder.Build();
    }
}
