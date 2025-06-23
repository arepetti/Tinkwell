using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.IO;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Supervisor.Commands;
using Tinkwell.Supervisor.Sentinel;

namespace Tinkwell.Supervisor;

sealed class Worker : IHostedService
{
    public Worker(IHost host, ILogger<Worker> logger, IConfiguration configuration, IFileSystem fileSystem, IRegistry registry, ICommandServer commandServer)
    {
        _host = host;
        _logger = logger;
        _fileSystem = fileSystem;
        _registry = registry;
        _commandServer = commandServer;

        _ensambleFilePath = fileSystem.ResolveFullPath(
            configuration.GetValue<string?>("Ensamble:Path") ?? "./system.ensamble");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!await _fileSystem.FileExistsAsync(_ensambleFilePath, cancellationToken))
        {
            await PanicAsync($"Ensamble file not found: '{_ensambleFilePath}'.");
            return;
        }

        await _registry.StartAsync(_ensambleFilePath, cancellationToken);
        await _commandServer.StartAsync(cancellationToken);

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
    private readonly IFileSystem _fileSystem;
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
                .AddTransient<IFileSystem, PhysicalFileSytem>()
                .AddTransient<IEnsambleFileReader, EnsambleFileReader>()
                .AddSingleton<IRegistry, Registry>()
                .AddTransient<IChildProcessBuilder, SentinelProcessBuilder>()
                .AddTransient<INamedPipeServerFactory, NamedPipeServerFactory>()
                .AddSingleton<ICommandServer, Server>()
                .AddHostedService<Worker>();
        });
    }
}
