using System.Collections.Concurrent;

namespace Tinkwell.EventsGateway;

sealed class Broker : IBroker
{
    public event EventHandler<EnqueuedEventArgs>? Enqueued;

    public void Publish(EventData data)
    {
        _queue.Add(data);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_worker is not null || cancellationToken.IsCancellationRequested)
            return Task.CompletedTask;

        _cts = new CancellationTokenSource();
        _worker = Task.Factory.StartNew(() =>
        {
            try
            {
                foreach (var data in _queue.GetConsumingEnumerable(_cts.Token))
                    Enqueued?.Invoke(this, new EnqueuedEventArgs(data));
            }
            catch (OperationCanceledException) 
            {
            }
        }, TaskCreationOptions.LongRunning);
    
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_worker is null)
            return;

        _queue.CompleteAdding();
        await _cts!.CancelAsync();
        await _worker!.WaitAsync(cancellationToken);
        _worker = null;
    }

    private readonly BlockingCollection<EventData> _queue = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
}