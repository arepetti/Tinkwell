using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Runlet.Store;

/// <summary>
/// Background service that reads <see cref="StoreEvent"/>s from the
/// notification channel and fans them out to matching subscribers.
/// Drains remaining events on shutdown.
/// </summary>
internal sealed class NotificationWorker : BackgroundService
{
    private readonly StoreNotifier _notifier;
    private readonly ILogger<NotificationWorker> _logger;

    public NotificationWorker(StoreNotifier notifier, ILogger<NotificationWorker> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Notification worker started");

        try
        {
            await foreach (var e in _notifier.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await _notifier.FanOutAsync(e);
                }
                catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Error during notification fan-out for {EventType} {BucketId}/{Namespace}/{Key}",
                        e.Type, e.BucketId, e.KeyNamespace, e.Key);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown — fall through to drain.
        }

        await DrainAsync();

        _logger.LogDebug("Notification worker stopped");
    }

    private async Task DrainAsync()
    {
        while (_notifier.Reader.TryRead(out var e))
        {
            try
            {
                await _notifier.FanOutAsync(e);
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error draining notification for {EventType} {BucketId}/{Namespace}/{Key}",
                    e.Type, e.BucketId, e.KeyNamespace, e.Key);
            }
        }
    }
}