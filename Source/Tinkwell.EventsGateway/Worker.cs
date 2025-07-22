using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper;

namespace Tinkwell.EventsGateway;

sealed class Worker(ILogger<Worker> logger) : BackgroundService, IBroker
{
    public event EventHandler<EnqueuedEventArgs>? Enqueued;

    public void Publish(EventData data)
        => _queue.Add(data);

    public override  async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.CompleteAdding();
        await base.StopAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _worker.StartAsync((cancellationToken) =>
        {
            _logger.LogDebug("Events Gateway started successfully.");
            foreach (var data in _queue.GetConsumingEnumerable(cancellationToken))
                Enqueued?.Invoke(this, new EnqueuedEventArgs(data));
        }, stoppingToken);
    }

    private readonly ILogger<Worker> _logger = logger;
    private readonly BlockingCollection<EventData> _queue = new();
    private readonly CancellableLongRunningTask _worker = new();
}

