namespace Tinkwell.Supervisor.Commands;

interface ICommandServer
{
    string ServerId { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
