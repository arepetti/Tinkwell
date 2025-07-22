using Microsoft.Extensions.Hosting;

namespace Tinkwell.Watchdog;

sealed class Worker(IWatchdog watchdog) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _watchdog.ExecuteAsync(stoppingToken);

    private readonly IWatchdog _watchdog = watchdog;
}
