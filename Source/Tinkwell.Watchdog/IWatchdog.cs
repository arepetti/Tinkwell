namespace Tinkwell.Watchdog;

interface IWatchdog : IAsyncDisposable
{
    Snapshot[] GetSnapshots();

    Task ExecuteAsync(CancellationToken stoppingToken);
}
