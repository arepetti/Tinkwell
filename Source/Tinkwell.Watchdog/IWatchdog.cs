namespace Tinkwell.Watchdog;

interface IWatchdog : IAsyncDisposable
{
    bool? IsLatestSampleAnAnomaly { get; }

    Snapshot[] GetSnapshots();

    Task ExecuteAsync(CancellationToken stoppingToken);
}
