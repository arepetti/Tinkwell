using System.Collections.Concurrent;
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
        _endpoints = new Endpoints(configuration);

        _enabled = configuration.GetValue("Supervisor:CommandServer:Enabled", true);
        _pipeName = configuration.GetValue("Supervisor:CommandServer:PipeName",
            WellKnownNames.SupervisorCommandServerPipeName);
        _maxConcurrentConnections = configuration.GetValue("Supervisor:CommandServer:MaxConcurrentConnections", 4);
    }

    public event EventHandler? Signaled;

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
        interpreter.Signaled += (_, _) => Signaled?.Invoke(this, EventArgs.Empty);
        interpreter.ClaimUrl += (_, e) => e.Value = _endpoints.Claim(e.MachineName, e.Runner);
        interpreter.QueryUrl += (_, e) => e.Value = _endpoints.Query(e.Runner);
        interpreter.ClaimRole += (_, e) =>
        {
            if (_roles.TryGetValue(e.Role!, out string? masterAddress))
            {
                e.Value = masterAddress;
                return;
            }
            
            if (!_roles.TryAdd(e.Role!, _endpoints.Query(e.Runner)!))
                e.Value = _roles[e.Role!];
        };
        interpreter.QueryRole += (_, e) =>
        {
            if (_roles.TryGetValue(e.Role!, out string? masterAddress))
            {
                e.Value = masterAddress;
                return;
            }
        };
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
    private readonly Endpoints _endpoints;
    private readonly ConcurrentDictionary<string, string> _roles = new();
}
