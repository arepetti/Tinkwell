namespace Tinkwell.Supervisor.Commands;

interface ICommandServer
{
    event EventHandler? Signaled;
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
