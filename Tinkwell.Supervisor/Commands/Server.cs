using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Supervisor.Commands;

sealed class Server : ICommandServer
{
    public Server(ILogger<Server> logger, IConfiguration configuration, IRegistry registry, INamedPipeServerFactory pipeServerFactory)
    {
        _logger = logger;
        _registry = registry;
        _pipeServer = pipeServerFactory.Create();

        _enabled = configuration.GetValue("Supervisor::CommandServer:Enabled", true);
        _pipeName = configuration.GetValue("Supervisor::CommandServer:PipeName",
            WellKnownNames.SupervisorCommandServerPipeName);
        _maxConcurrentConnections = configuration.GetValue("Supervisor::CommandServer:MaxConcurrentConnections", 4);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_enabled)
            return Task.CompletedTask;

        _logger.LogDebug("Starting command server on pipe '{PipeName}'", _pipeName);
        _pipeServer.MaxConcurrentConnections = _maxConcurrentConnections;
        _pipeServer.Process += ReadAndProcessPipeData;
        _pipeServer.Open(_pipeName);

        return Task.CompletedTask;
    }

    private void ReadAndProcessPipeData(object? sender, NamedPipeServerProcessEventArgs e)
    {
        var interpreter = new Interpreter(_logger, _registry);
        interpreter.ReadAndProcessNextCommandAsync(e.Reader, e.Writer, e.CancellationToken)
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                    _logger.LogError(task.Exception, "Error processing command from pipe '{PipeName}'", _pipeName);
                
                if (task.IsFaulted || task.Result == Interpreter.ParsingResult.Stop)
                    e.Disconnect();
            }, TaskScheduler.Default)
            .GetAwaiter()
            .GetResult();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _pipeServer.Close();
        return Task.CompletedTask;
    }

    private readonly ILogger<Server> _logger;
    private readonly IRegistry _registry;
    private readonly INamedPipeServer _pipeServer;
    private readonly string _pipeName;
    private readonly int _maxConcurrentConnections;
    private readonly bool _enabled;
}
