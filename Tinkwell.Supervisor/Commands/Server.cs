using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Supervisor.Commands;

sealed class Server : ICommandServer
{
    public Server(ILogger<Server> logger, IConfiguration configuration, IRegistry registry)
    {
        _logger = logger;
        _configuration = configuration;
        _registry = registry;

        _enabled = configuration.GetValue("Supervisor::CommandServer:Enabled", true);
        ServerId = configuration.GetValue("Supervisor::CommandServer:PipeName",
            WellKnownNames.SupervisorCommandServerPipeName);
    }

    public string ServerId { get; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_enabled)
            return Task.CompletedTask;

        _abortTokenSource = new CancellationTokenSource();

        CreatePipe();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_abortTokenSource is null)
            return Task.CompletedTask;

        _abortTokenSource.Cancel();
        _abortTokenSource.Dispose();

        return Task.CompletedTask;
    }

    private readonly ILogger<Server> _logger;
    private readonly IConfiguration _configuration;
    private readonly IRegistry _registry;
    private readonly bool _enabled;
    private CancellationTokenSource? _abortTokenSource;
    private readonly List<Pipe> _pipes = [];

    private void CreatePipe()
    {
        var pipe = new Pipe(
            ServerId,
            _abortTokenSource!,
            _logger,
            _configuration,
            _registry
        );

        pipe.Connected += (_, _) => CreatePipe();
        pipe.Disconnected += (sender, _) =>
        {
            lock (_pipes)
                _pipes.Remove((Pipe)sender!);
        };

        lock (_pipes)
            _pipes.Add(pipe);

        _logger.LogDebug("Starting a new command pipe, {Count} active pipes", _pipes.Count);
        pipe.Start();
    }
}
