using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Tasks;
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

        _pipeName = configuration.GetValue("Supervisor:CommandServer:PipeName",
            WellKnownNames.SupervisorCommandServerPipeName);
        _maxConcurrentConnections = configuration.GetValue("Supervisor:CommandServer:MaxConcurrentConnections", 4);
    }

    public event EventHandler? Signaled;

    public bool IsReady { get; set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting command server on pipe '{PipeName}'", _pipeName);
        _pipeServer.MaxConcurrentConnections = _maxConcurrentConnections;
        _pipeServer.ProcessAsync = ReadAndProcessPipeDataAsync;
        _pipeServer.Open(_pipeName);

        return Task.CompletedTask;
    }

    private async Task ReadAndProcessPipeDataAsync(NamedPipeServerProcessEventArgs e)
    {
        try
        {
            var interpreter = new Interpreter(_logger, _registry);
            interpreter.IsReady = IsReady;
            interpreter.Signaled += (_, _) => Signaled?.Invoke(this, EventArgs.Empty);
            interpreter.ClaimUrl += (_, e) => e.Value = _endpoints.Claim(e.MachineName, e.Runner);
            interpreter.QueryUrl += (_, e) =>
            {
                if (e.Inverted)
                    e.Value = _endpoints.InverseQuery(e.Runner); // Yep, we get the URL from here, TODO: Add different EventArgs?
                else
                    e.Value = _endpoints.Query(e.Runner);
            };
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

            var result = await interpreter.ReadAndProcessNextCommandAsync(e.Reader, e.Writer, e.CancellationToken);
            if (result == Interpreter.ParsingResult.Stop)
                e.Disconnect();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Error processing pipe data: {Reason}", exception.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _pipeServer.Close();
        return Task.CompletedTask;
    }

    public string? QueryRole(string roleName)
    {
        if (_roles.TryGetValue(roleName, out string? masterAddress))
            return masterAddress;

        return null;
    }

    private readonly ILogger<Server> _logger;
    private readonly IRegistry _registry;
    private readonly INamedPipeServer _pipeServer;
    private readonly string _pipeName;
    private readonly int _maxConcurrentConnections;
    private readonly Endpoints _endpoints;
    private readonly ConcurrentDictionary<string, string> _roles = new();
}
