namespace Tinkwell.Supervisor.Commands;

interface ICommandServer
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
