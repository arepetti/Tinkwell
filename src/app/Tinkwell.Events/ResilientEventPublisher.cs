using Microsoft.Extensions.Logging;

namespace Tinkwell.Events;

/// <summary>
/// Wraps an inner publisher delegate and retries with re-discovery when the
/// event bus is unavailable. Catches gRPC <c>StatusCode.Unavailable</c> and
/// asks the supplied factory to create a fresh delegate (typically after
/// re-discovering the event bus endpoint).
/// </summary>
public sealed class ResilientEventPublisher : IEventPublisher
{
    private readonly Func<CancellationToken, Task<Func<EventEnvelope, CancellationToken, Task>?>> _factory;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private volatile Func<EventEnvelope, CancellationToken, Task>? _delegate;

    /// <param name="initialDelegate">
    /// The delegate resolved during startup (may be <see langword="null"/> if
    /// the event bus was not discovered).
    /// </param>
    /// <param name="factory">
    /// Async factory that re-discovers the event bus and returns a fresh
    /// publish delegate, or <see langword="null"/> if still unavailable.
    /// </param>
    /// <param name="logger">Logger instance.</param>
    public ResilientEventPublisher(
        Func<EventEnvelope, CancellationToken, Task>? initialDelegate,
        Func<CancellationToken, Task<Func<EventEnvelope, CancellationToken, Task>?>> factory,
        ILogger logger)
    {
        _delegate = initialDelegate;
        _factory = factory;
        _logger = logger;
    }

    public async Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default)
    {
        var del = _delegate;
        if (del is null)
        {
            del = await TryReconnectAsync(ct);
            if (del is null)
                return;
        }

        try
        {
            await del(envelope, ct);
        }
        catch (Grpc.Core.RpcException rpc) when (rpc.StatusCode == Grpc.Core.StatusCode.Unavailable)
        {
            _logger.LogWarning("Event bus unavailable, attempting reconnect");

            del = await TryReconnectAsync(ct);
            if (del is null)
            {
                _logger.LogWarning("Event bus reconnect failed — event dropped");
                return;
            }

            await del(envelope, ct);
        }
    }

    private async Task<Func<EventEnvelope, CancellationToken, Task>?> TryReconnectAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var fresh = await _factory(ct);
            _delegate = fresh;
            if (fresh is not null)
                _logger.LogInformation("Event bus reconnected");
            return fresh;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Event bus re-discovery failed");
            _delegate = null;
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }
}