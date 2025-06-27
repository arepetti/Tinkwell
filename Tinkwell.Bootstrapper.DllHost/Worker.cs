using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;

namespace Tinkwell.Bootstrapper.DllHost;

sealed class Worker : IHostedService
{
    public Worker(IHost host, ILogger<Worker> logger, IConfiguration configuration, INamedPipeClient pipeClient)
    {
        _host = host;
        _logger = logger;
        _configuration = configuration;
        _pipeClient = pipeClient;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DllHost '{HostName}' started successfully", Extensions.RunnerName);
        await _pipeClient.SendCommandToSupervisorAndDisconnectAsync(_configuration, $"signal \"{Extensions.RunnerName}\"");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DllHost '{HostName}' stopped successfully", Extensions.RunnerName);
        return Task.CompletedTask;
    }

    private readonly IHost _host;
    private readonly IConfiguration _configuration;
    private readonly ILogger<Worker> _logger;
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
                .AddHostedService<Worker>();
        });
    }
}
