namespace Tinkwell.Supervisor.Commands;

interface ICommandServer
{
    event EventHandler? Signaled;

    bool IsReady { get; set; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    string? QueryRole(string roleName);
}
