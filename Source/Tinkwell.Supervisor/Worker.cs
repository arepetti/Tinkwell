using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Supervisor.Commands;
using Tinkwell.Supervisor.Sentinel;

namespace Tinkwell.Supervisor;

sealed class Worker : IHostedService
{
    public Worker(IHost host, ILogger<Worker> logger, IConfiguration configuration, IRegistry registry, ICommandServer commandServer)
    {
        _host = host;
        _logger = logger;
        _registry = registry;
        _commandServer = commandServer;

        _ensambleFilePath = HostingInformation.GetFullPath(
            configuration.GetValue<string?>("Ensamble:Path") ?? "./ensamble.tw");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Application version: {Version}", HostingInformation.ApplicationVersion);
        _logger.LogDebug("OS: {OS} ({Architecture}). Process: {Process}, Runtime: {Runtime}",
            RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture,
            RuntimeInformation.ProcessArchitecture, RuntimeInformation.FrameworkDescription);
        _logger.LogDebug("Working directory: {Path}", HostingInformation.WorkingDirectory);
        _logger.LogDebug("Current directory: {Path}", Environment.CurrentDirectory);
        _logger.LogDebug("Executables directory: {Path}", StrategyAssemblyLoader.GetAppPath());

        if (!File.Exists(_ensambleFilePath))
        {
            await PanicAsync($"Ensamble file not found: '{_ensambleFilePath}'.");
            return;
        }

        await _commandServer.StartAsync(cancellationToken);
        await _registry.StartAsync(_commandServer, _ensambleFilePath, cancellationToken);

        _commandServer.IsReady = true;
        _logger.LogInformation("Supervisor started successfully");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _registry.StopAsync(cancellationToken);
        await _commandServer.StopAsync(cancellationToken);

        _logger.LogInformation("Supervisor stopped successfully");
    }

    private readonly IHost _host;
    private readonly ILogger<Worker> _logger;
    private readonly IRegistry _registry;
    private readonly ICommandServer _commandServer;
    private readonly string _ensambleFilePath;

    private Task PanicAsync(string message)
    {
        _logger.LogCritical(message);
        return _host.StopAsync(TimeSpan.FromSeconds(5));
    }
}

static class WorkerExtensions
{
    public static IHostBuilder AddWorker(this IHostBuilder builder)
    {
        return builder.ConfigureServices((context, services) =>
        {
            services
                .AddSingleton<IExpressionEvaluator, ExpressionEvaluator>()
                .AddTransient<IEnsambleConditionEvaluator, EnsambleConditionEvaluator>()
                .AddTransient<IConfigFileReader<IEnsambleFile>, EnsambleFileReader>()
                .AddSingleton<IRegistry, Registry>()
                .AddTransient<IChildProcessBuilder, SentinelProcessBuilder>()
                .AddTransient<INamedPipeServerFactory, NamedPipeServerFactory>()
                .AddSingleton<ICommandServer, Server>()
                .AddHostedService<Worker>();
        });
    }
}
