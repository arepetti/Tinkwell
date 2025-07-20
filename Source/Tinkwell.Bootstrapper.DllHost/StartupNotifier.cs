using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;

namespace Tinkwell.Bootstrapper.DllHost;

sealed class StartupNotifier : IHostedService
{
    public StartupNotifier(IHostApplicationLifetime lifetime, ILogger<StartupNotifier> logger, IConfiguration configuration, INamedPipeClient pipeClient)
    {
        _lifetime = lifetime;
        _logger = logger;
        _configuration = configuration;
        _pipeClient = pipeClient;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting DllHost '{HostName}'.", HostingInformation.RunnerName);
        _lifetime.ApplicationStarted.Register(() =>
        {
            _logger.LogInformation("DllHost '{HostName}' started successfully", HostingInformation.RunnerName);

            string command = $"signal \"{HostingInformation.RunnerName}\"";
            _pipeClient.SendCommandToSupervisorAndDisconnect(_configuration, command);
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DllHost '{HostName}' stopped successfully", HostingInformation.RunnerName);
        return Task.CompletedTask;
    }

    private readonly IHostApplicationLifetime _lifetime;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly INamedPipeClient _pipeClient;
}

static class WorkerExtensions
{
    public static IHostBuilder AddWorker(this IHostBuilder builder)
    {
        return builder.ConfigureServices((context, services) =>
        {
            services
                .AddTransient<IActivity, RegisterDllsActivity>()
                .AddTransient<INamedPipeClient, NamedPipeClient>()
                .AddTransient<IExpressionEvaluator, ExpressionEvaluator>()
                .AddTransient<IEnsambleConditionEvaluator, EnsambleConditionEvaluator>()
                .AddHostedService<StartupNotifier>();
        });
    }
}
