using System.Collections.Concurrent;
using Grpc.Core.Logging;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper;

namespace Tinkwell.EventsGateway;

sealed class Broker(ILogger<Broker> logger) : IBroker
{
    public event EventHandler<EnqueuedEventArgs>? Enqueued;

    public void Publish(EventData data)
    {
        _queue.Add(data);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_worker.IsRunning || cancellationToken.IsCancellationRequested)
            return;

        await _worker.StartAsync(cancellationToken =>
        {
            foreach (var data in _queue.GetConsumingEnumerable(cancellationToken))
                Enqueued?.Invoke(this, new EnqueuedEventArgs(data));
        });

        _logger.LogInformation("Events Gateway Broker started successfully.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.CompleteAdding();
        await _worker.StopAsync(cancellationToken);
    }

    private readonly ILogger<Broker> _logger = logger;
    private readonly BlockingCollection<EventData> _queue = new();
    private readonly CancellableLongRunningTask _worker = new();
}
