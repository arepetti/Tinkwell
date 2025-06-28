using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Reducer;

sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly Reducer _reducer;

    public Worker(ILogger<Worker> logger, Reducer reducer)
    {
        _logger = logger;
        _reducer = reducer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reducer worker running at: {time}", DateTimeOffset.Now);
        await _reducer.StartAsync(stoppingToken);
    }
}
