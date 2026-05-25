using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Runlet.Store.Backend;

namespace Tinkwell.Runlet.Store;

/// <summary>
/// Periodically sweeps the backend for expired entries and publishes
/// <see cref="StoreEventType.Expired"/> notifications for each one.
/// </summary>
internal sealed class ExpirationService : BackgroundService
{
    private readonly IStoreBackend _backend;
    private readonly StoreNotifier _notifier;
    private readonly ILogger<ExpirationService> _logger;
    private readonly TimeSpan _interval;

    public ExpirationService(
        IStoreBackend backend,
        StoreNotifier notifier,
        ILogger<ExpirationService> logger,
        TimeSpan interval)
    {
        _backend = backend;
        _notifier = notifier;
        _logger = logger;
        _interval = interval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Expiration service started (interval: {Interval})", _interval);

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var expired = await _backend.CleanupExpiredAsync();

                if (expired.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired entries", expired.Count);

                    foreach (var entry in expired)
                    {
                        _notifier.Enqueue(new StoreEvent(
                            StoreEventType.Expired,
                            entry.BucketId,
                            entry.KeyNamespace,
                            entry.Key,
                            null,
                            entry.CreatedAt,
                            entry.UpdatedAt,
                            entry.ExpiresAt));
                    }
                }
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Error during expiration sweep");
            }
        }
    }
}