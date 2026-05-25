using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tinkwell.Coap.Server;

/// <summary>
/// A standalone CoAP server (RFC 7252) with resource routing and RFC 7641 Observe support.
/// </summary>
/// <remarks>
/// <para>
/// Register handlers with <see cref="MapGet(string,Func{CoapRequest,CancellationToken,Task{CoapResponse}})"/>,
/// <see cref="MapPut(string,Func{CoapRequest,CancellationToken,Task{CoapResponse}})"/>,
/// <see cref="MapPost(string,Func{CoapRequest,CancellationToken,Task{CoapResponse}})"/>, and
/// <see cref="MapDelete(string,Func{CoapRequest,CancellationToken,Task{CoapResponse}})"/> before the server starts,
/// then call <see cref="RunAsync"/> or let the .NET Generic Host start it as a
/// <see cref="BackgroundService"/>. The server binds a single UDP socket in dual-stack mode (IPv4
/// and IPv6 on the same port).
/// </para>
/// <para>
/// <b>Route registration is not thread-safe</b> with respect to request processing. All routes
/// must be registered before <see cref="RunAsync"/>/<see cref="ExecuteAsync"/> begins; calling
/// <c>Map*</c> after the server has started throws <see cref="InvalidOperationException"/>.
/// Routes are evaluated in registration order, so more specific patterns should be registered
/// before broader ones (see <see cref="CoapPathMatcher"/> for wildcard semantics).
/// </para>
/// <para>
/// <b>Blockwise transfers (RFC 7959):</b> transparent Block1 reassembly and Block2 splitting are
/// provided out of the box. Incoming chunked uploads are reassembled before the handler runs
/// (the handler is invoked <i>once</i>, with the full payload); handler responses larger than the
/// configured block size are split, cached for <see cref="CoapServerOptions.Block2CacheTtl"/>,
/// and served block-by-block to follow-up requests. Handlers that need custom blockwise logic can
/// opt out by setting <see cref="CoapResponse.Block2"/> themselves, and the feature can be fully
/// disabled per server via <see cref="CoapServerOptions.ResponseBlockSize"/> and
/// <see cref="CoapServerOptions.Block1MaxPayloadBytes"/>. Large Observe notifications are <i>not</i>
/// split (RFC 7959, Section 3.4); they are sent as a single datagram.
/// </para>
/// <para>Example:</para>
/// <code>
/// var server = new CoapServer(new CoapServerOptions { Port = 5683 });
/// server.MapGet("/hello", (req, ct) =>
///     Task.FromResult(CoapResponse.Content("Hi!"u8.ToArray(), CoapContentFormat.TextPlain)));
/// await server.RunAsync(CancellationToken.None);
/// </code>
/// </remarks>
public sealed class CoapServer : BackgroundService, IAsyncDisposable
{
    private readonly CoapServerOptions _options;
    private readonly ILogger _logger;
    private readonly ObserverRegistry _observers = new();
    private readonly List<RouteEntry> _routes = [];
    private readonly List<ICoapRequestExceptionFilter> _requestExceptionFilters = [];
    private readonly List<ICoapDatagramExceptionFilter> _datagramExceptionFilters = [];
    private readonly SemaphoreSlim _concurrency;
    private readonly BlockwiseCoordinator _coordinator;
    private readonly MessageIdDeduplicator _dedup;
    private readonly TimeProvider _timeProvider;

    private readonly Channel<string> _notifyChannel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    private long _droppedRequests;
    private long _droppedNotifications;
    private int _pendingRequests;
    private int _inFlightProcessing;
    private int _started;
    private int _boundPort;
    private bool _disposed;

    /// <summary>
    /// Creates a new CoAP server with the given options and optional logger.
    /// </summary>
    /// <param name="options">Server configuration (port, concurrency, etc.). Cannot be <see langword="null"/>.</param>
    /// <param name="logger">Optional logger. Defaults to a no-op logger when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    public CoapServer(CoapServerOptions options, ILogger<CoapServer>? logger = null)
        : this(options, logger, TimeProvider.System)
    {
    }

    internal CoapServer(CoapServerOptions options, ILogger<CoapServer>? logger, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _options = options;
        _logger = logger ?? NullLogger<CoapServer>.Instance;
        _timeProvider = timeProvider;
        _concurrency = new SemaphoreSlim(options.MaxConcurrentRequests, options.MaxConcurrentRequests);
        _coordinator = new BlockwiseCoordinator(options, timeProvider);
        _dedup = new MessageIdDeduplicator(options, timeProvider);
    }

    /// <summary>The Observe registry, exposed for advanced scenarios.</summary>
    /// <remarks>
    /// Direct access lets you inspect active observers, force-deregister specific clients, or
    /// remove all observers from a given endpoint (useful after authentication/authorization
    /// failures). Most applications should only call <see cref="NotifyObservers(string)"/>.
    /// </remarks>
    public ObserverRegistry Observers => _observers;

    /// <summary>
    /// Number of requests dropped because the pending queue exceeded
    /// <see cref="CoapServerOptions.MaxPendingRequests"/>.
    /// </summary>
    /// <remarks>
    /// This counter is monotonic and read atomically. Exposed for diagnostics and external
    /// metrics systems. Dropped requests are responded to with <c>5.03 Service Unavailable</c> when
    /// they are Confirmable.
    /// </remarks>
    public long DroppedRequests => Interlocked.Read(ref _droppedRequests);

    /// <summary>
    /// Number of Observe notifications dropped because the internal notification queue was full.
    /// </summary>
    /// <remarks>
    /// The notification queue has a fixed capacity (512) and applies a drop-on-full policy. A
    /// non-zero value indicates that a resource is churning faster than the network can deliver
    /// notifications, or that too many observers are attached to a single resource.
    /// </remarks>
    public long DroppedNotifications => Interlocked.Read(ref _droppedNotifications);

    /// <summary>
    /// The UDP port that the server is actually bound to. <c>0</c> before the server starts.
    /// </summary>
    /// <remarks>
    /// When <see cref="CoapServerOptions.Port"/> is set to <c>0</c>, the OS picks an ephemeral
    /// port; this property exposes the chosen port so tests and clients can discover it.
    /// </remarks>
    public int BoundPort => Volatile.Read(ref _boundPort);

    /// <summary>
    /// Registers a handler for CoAP <c>GET</c> requests matching <paramref name="pattern"/>.
    /// </summary>
    /// <param name="pattern">Path pattern; see <see cref="CoapPathMatcher"/> for wildcard rules.</param>
    /// <param name="handler">The handler to invoke when a matching GET request arrives.</param>
    /// <returns>This <see cref="CoapServer"/> instance, to allow fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="pattern"/> or <paramref name="handler"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if called after the server has started (routes must be registered up-front).
    /// </exception>
    /// <example>
    /// <para>Expose a read-only temperature resource as plain text (LwM2M-style path).</para>
    /// <code>
    /// server.MapGet("/3303/0/5700", (req, ct) =>
    ///     Task.FromResult(
    ///         CoapResponse.Content("21.3"u8.ToArray(), CoapContentFormat.TextPlain)));
    /// </code>
    /// </example>
    public CoapServer MapGet(string pattern, Func<CoapRequest, CancellationToken, Task<CoapResponse>> handler)
        => MapMethod(CoapMethod.Get, pattern, handler);

    /// <summary>Registers a handler for CoAP <c>PUT</c> requests matching <paramref name="pattern"/>.</summary>
    /// <example>
    /// <para>Writable resource: require a non-empty body, then apply the update and return <c>2.04 Changed</c>.</para>
    /// <code>
    /// server.MapPut("/actuators/0/value", (req, ct) =>
    ///     Task.FromResult(
    ///         req.Payload.IsEmpty ? CoapResponse.BadRequest() : CoapResponse.Changed()));
    /// </code>
    /// </example>
    /// <inheritdoc cref="MapGet(string, Func{CoapRequest, CancellationToken, Task{CoapResponse}})"/>
    public CoapServer MapPut(string pattern, Func<CoapRequest, CancellationToken, Task<CoapResponse>> handler)
        => MapMethod(CoapMethod.Put, pattern, handler);

    /// <summary>Registers a handler for CoAP <c>POST</c> requests matching <paramref name="pattern"/>.</summary>
    /// <example>
    /// <para>Create a new sub-resource under a collection and return <c>2.01 Created</c> with a body.</para>
    /// <code>
    /// server.MapPost("/devices", (req, ct) =>
    ///     Task.FromResult(
    ///         CoapResponse.Created("device-7"u8.ToArray(), CoapContentFormat.TextPlain)));
    /// </code>
    /// </example>
    /// <inheritdoc cref="MapGet(string, Func{CoapRequest, CancellationToken, Task{CoapResponse}})"/>
    public CoapServer MapPost(string pattern, Func<CoapRequest, CancellationToken, Task<CoapResponse>> handler)
        => MapMethod(CoapMethod.Post, pattern, handler);

    /// <summary>Registers a handler for CoAP <c>DELETE</c> requests matching <paramref name="pattern"/>.</summary>
    /// <example>
    /// <para>Remove a resource and confirm with <c>2.02 Deleted</c>.</para>
    /// <code>
    /// server.MapDelete("/alarms/3", (req, ct) =>
    ///     Task.FromResult(CoapResponse.Deleted()));
    /// </code>
    /// </example>
    /// <inheritdoc cref="MapGet(string, Func{CoapRequest, CancellationToken, Task{CoapResponse}})"/>
    public CoapServer MapDelete(string pattern, Func<CoapRequest, CancellationToken, Task<CoapResponse>> handler)
        => MapMethod(CoapMethod.Delete, pattern, handler);

    /// <summary>
    /// Registers a handler for any CoAP method matching <paramref name="pattern"/>.
    /// </summary>
    /// <param name="pattern">Path pattern; see <see cref="CoapPathMatcher"/>.</param>
    /// <param name="handler">Handler invoked for every method on the matching path.</param>
    /// <returns>This <see cref="CoapServer"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="pattern"/> or <paramref name="handler"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if called after the server has started.
    /// </exception>
    /// <example>
    /// <para>Register a class-based handler to branch on <see cref="CoapRequest.Method"/> for one path pattern.</para>
    /// <code>
    /// server.Map("/sensors/*", new SensorResourceHandler());
    /// </code>
    /// </example>
    public CoapServer Map(string pattern, ICoapRequestHandler handler)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(handler);
        EnsureNotStarted();
        _routes.Add(new RouteEntry(null, pattern, new DelegateHandler(
            (req, ct) => handler.HandleAsync(req, ct))));
        return this;
    }

    /// <summary>
    /// Registers an exception filter that runs when a route handler throws and may override the
    /// default <c>5.00 Internal Server Error</c> response.
    /// </summary>
    /// <param name="filter">The filter to register.</param>
    /// <returns>This <see cref="CoapServer"/> instance, to allow fluent chaining.</returns>
    /// <remarks>
    /// Filters run in registration order; the first filter that returns a non-<see langword="null"/>
    /// response wins and short-circuits the chain. See <see cref="ICoapRequestExceptionFilter"/>
    /// for the full ordering and isolation contract.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filter"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if called after the server has started.</exception>
    /// <example>
    /// <para>Install a class that maps known exceptions to CoAP error codes and logs the rest.</para>
    /// <code>
    /// public sealed class ErrorMappingFilter(ILogger logger) : ICoapRequestExceptionFilter
    /// {
    ///     public Task&lt;CoapResponse?&gt; OnExceptionAsync(
    ///         CoapRequestExceptionContext c, CancellationToken ct)
    ///     {
    ///         if (c.Exception is not KeyNotFoundException) logger.LogError(c.Exception, c.Request.Path);
    ///         return Task.FromResult(c.Exception is KeyNotFoundException
    ///             ? CoapResponse.NotFound() : (CoapResponse?)null);
    ///     }
    /// }
    ///
    /// server.UseRequestExceptionFilter(new ErrorMappingFilter(logger));
    /// </code>
    /// </example>
    public CoapServer UseRequestExceptionFilter(ICoapRequestExceptionFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        EnsureNotStarted();
        _requestExceptionFilters.Add(filter);
        return this;
    }

    /// <summary>
    /// Registers a delegate-based request exception filter. Equivalent to wrapping the delegate in
    /// an internal adapter that implements <see cref="ICoapRequestExceptionFilter"/>.
    /// </summary>
    /// <param name="handler">Delegate invoked with the exception context.</param>
    /// <returns>This <see cref="CoapServer"/> instance, to allow fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if called after the server has started.</exception>
    /// <example>
    /// <para>Log the failure, then return <see langword="null"/> so a later filter (or the default <c>5.00</c>) applies.</para>
    /// <code>
    /// server.UseRequestExceptionFilter(
    ///     (ctx, ct) =>
    ///     {
    ///         logger.LogError(ctx.Exception, "CoAP {Path} failed", ctx.Request.Path);
    ///         return Task.FromResult&lt;CoapResponse?&gt;(null);
    ///     });
    /// </code>
    /// </example>
    public CoapServer UseRequestExceptionFilter(
        Func<CoapRequestExceptionContext, CancellationToken, Task<CoapResponse?>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return UseRequestExceptionFilter(new DelegateRequestExceptionFilter(handler));
    }

    /// <summary>
    /// Registers a filter that observes faults raised by the datagram pipeline outside route
    /// handlers (parse-time exceptions other than <see cref="FormatException"/>, blockwise
    /// coordinator faults, transport-send faults, and so on).
    /// </summary>
    /// <param name="filter">The filter to register.</param>
    /// <returns>This <see cref="CoapServer"/> instance, to allow fluent chaining.</returns>
    /// <remarks>
    /// All registered filters run in registration order (observer fan-out). See
    /// <see cref="ICoapDatagramExceptionFilter"/> for the full contract.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filter"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if called after the server has started.</exception>
    /// <example>
    /// <para>Record pipeline-side failures (blockwise, send, etc.) for diagnostics; this hook does not send a CoAP response.</para>
    /// <code>
    /// public sealed class LogDatagramFaults(ILogger log) : ICoapDatagramExceptionFilter
    /// {
    ///     public Task OnExceptionAsync(CoapDatagramExceptionContext c, CancellationToken ct)
    ///     {
    ///         log.LogError(c.Exception, "CoAP pipeline fault from {Endpoint}", c.RemoteEndpoint);
    ///         return Task.CompletedTask;
    ///     }
    /// }
    ///
    /// server.UseDatagramExceptionFilter(new LogDatagramFaults(logger));
    /// </code>
    /// </example>
    public CoapServer UseDatagramExceptionFilter(ICoapDatagramExceptionFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        EnsureNotStarted();
        _datagramExceptionFilters.Add(filter);
        return this;
    }

    /// <summary>
    /// Registers a delegate-based datagram exception filter. Equivalent to wrapping the delegate
    /// in an internal adapter that implements <see cref="ICoapDatagramExceptionFilter"/>.
    /// </summary>
    /// <param name="handler">Delegate invoked with the datagram fault context.</param>
    /// <returns>This <see cref="CoapServer"/> instance, to allow fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if called after the server has started.</exception>
    /// <example>
    /// <para>Log raw datagram faults for post-mortem; no CoAP error reply is sent at this layer.</para>
    /// <code>
    /// server.UseDatagramExceptionFilter(
    ///     (ctx, ct) =>
    ///     {
    ///         logger.LogError(ctx.Exception, "Datagram from {Endpoint}", ctx.RemoteEndpoint);
    ///         return Task.CompletedTask;
    ///     });
    /// </code>
    /// </example>
    public CoapServer UseDatagramExceptionFilter(
        Func<CoapDatagramExceptionContext, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return UseDatagramExceptionFilter(new DelegateDatagramExceptionFilter(handler));
    }

    /// <summary>
    /// Signals that the resource at <paramref name="path"/> has changed and all current observers
    /// must be notified.
    /// </summary>
    /// <param name="path">Absolute resource path whose observers should be notified (e.g. <c>"/3303/0/5700"</c>).</param>
    /// <remarks>
    /// <para>
    /// The notification is queued and delivered asynchronously by the background notifier loop;
    /// this method returns immediately. Each active observer on <paramref name="path"/> receives
    /// a fresh Non-confirmable response (RFC 7641, Section 3.2) produced by re-invoking the
    /// matching GET handler. If the internal queue is full the notification is dropped and
    /// counted against <see cref="DroppedNotifications"/>.
    /// </para>
    /// <para>
    /// This library sends Observe notifications as <c>NON</c> (non-confirmable) messages: the
    /// server does not retransmit them and does not wait for ACKs. Clients detecting missed
    /// notifications should re-register the Observe relation.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <para>After your domain model updates, push a fresh representation to all Observe subscribers on that path.</para>
    /// <code>
    /// temperatureC = ReadSensor();
    /// server.NotifyObservers("/3303/0/5700");
    /// </code>
    /// </example>
    public void NotifyObservers(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (_notifyChannel.Writer.TryWrite(path))
            return;

        var total = Interlocked.Increment(ref _droppedNotifications);
        if (total == 1 || total % 1000 == 0)
        {
            _logger.LogWarning(
                "CoAP server '{Name}' notification queue full - dropped {Count} notification(s) so far",
                _options.Name ?? "(default)", total);
        }
    }

    /// <summary>Starts the UDP listener and processes requests until <paramref name="ct"/> is cancelled.</summary>
    /// <param name="ct">Cancellation token that stops the server when triggered.</param>
    /// <returns>A task that completes when the server has fully stopped.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the server has already been started.</exception>
    /// <exception cref="SocketException">Thrown if the UDP socket cannot be bound (e.g. port already in use).</exception>
    /// <example>
    /// <para>Run the server in a console app without the generic host (no <c>IHostedService</c> registration).</para>
    /// <code>
    /// var server = new CoapServer(new CoapServerOptions { Port = 5683 });
    /// server.MapGet("/ping", (_, _) => Task.FromResult(CoapResponse.Content("pong"u8.ToArray(), CoapContentFormat.TextPlain)));
    /// await server.RunAsync(CancellationToken.None);
    /// </code>
    /// </example>
    public async Task RunAsync(CancellationToken ct) => await ExecuteAsync(ct).ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks>
    /// Invoked by the .NET Generic Host when the server is registered as a <see cref="BackgroundService"/>.
    /// Application code should prefer <see cref="RunAsync"/> when running standalone.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if the server has already been started.</exception>
    /// <exception cref="SocketException">Thrown if the UDP socket cannot be bound.</exception>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            throw new InvalidOperationException("CoapServer has already been started.");

        using var udp = CreateDualStackListener(_options.Port);
        var localEndpoint = (IPEndPoint)udp.Client.LocalEndPoint!;
        Volatile.Write(ref _boundPort, localEndpoint.Port);

        _logger.LogInformation("CoAP server '{Name}' listening on UDP port {Port}",
            _options.Name ?? "(default)", localEndpoint.Port);

        var notifierTask = RunNotifierAsync(udp, stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                UdpReceiveResult datagram;
                try
                {
                    datagram = await udp.ReceiveAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException ex)
                {
                    // On Windows, receiving an ICMP "port unreachable" from a previous Send triggers
                    // a ConnectionReset here (WSAECONNRESET). Those are benign - just keep listening.
                    _logger.LogDebug(ex, "UDP receive error (ignored)");
                    continue;
                }

                var pending = Interlocked.Increment(ref _pendingRequests);
                if (_options.MaxPendingRequests > 0 && pending > _options.MaxPendingRequests)
                {
                    Interlocked.Decrement(ref _pendingRequests);
                    Interlocked.Increment(ref _droppedRequests);
                    _ = SendServiceUnavailableAsync(udp, datagram, stoppingToken);
                    continue;
                }

                _ = ProcessDatagramAsync(udp, datagram, stoppingToken);
            }
        }
        finally
        {
            _notifyChannel.Writer.TryComplete();
            try { await notifierTask.ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
            }

            _logger.LogInformation("CoAP server '{Name}' stopped",
                _options.Name ?? "(default)");
        }
    }

    /// <summary>
    /// Stops the server (if running) and releases all resources: the UDP socket, the concurrency
    /// semaphore, and the Observe notification queue.
    /// </summary>
    /// <remarks>
    /// Safe to call multiple times and from a <c>await using</c> block. When the server is hosted
    /// by the .NET Generic Host the host manages the lifecycle and calling this method directly
    /// is unnecessary.
    /// </remarks>
    /// <example>
    /// <para>Ensure the UDP socket and background loops are torn down when leaving a scope.</para>
    /// <code>
    /// await using (var server = new CoapServer(CoapServerOptions.Default))
    /// {
    ///     await server.RunAsync(ct);
    /// }
    /// </code>
    /// </example>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        _notifyChannel.Writer.TryComplete();

        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Swallow: StopAsync cancels the execute task itself.
        }

        // StopAsync unblocks ExecuteAsync, but ProcessDatagramAsync runs fire-and-forget from
        // the receive loop. Drain any tasks still in flight before tearing down the coordinator
        // and semaphore, so those teardowns cannot race with a handler still reading blockwise
        // state.
        await DrainInFlightProcessingAsync().ConfigureAwait(false);

        base.Dispose();
        _concurrency.Dispose();
        _coordinator.Dispose();
        _dedup.Dispose();

        GC.SuppressFinalize(this);
    }

    private async Task DrainInFlightProcessingAsync()
    {
        // Typical steady state is a handful of tasks; poll briefly. The 5 s upper bound keeps a
        // stuck handler from hanging DisposeAsync forever; after that we fall through and let
        // remaining tasks fail naturally when they touch the disposed coordinator. Logged so
        // operators can spot it.
        var deadline = _timeProvider.GetUtcNow().AddSeconds(5);
        while (Volatile.Read(ref _inFlightProcessing) > 0
            && _timeProvider.GetUtcNow() < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
        }

        int remaining = Volatile.Read(ref _inFlightProcessing);
        if (remaining > 0)
        {
            _logger.LogWarning(
                "CoAP server '{Name}' disposed with {Count} request(s) still processing; those may observe disposal faults",
                _options.Name ?? "(default)", remaining);
        }
    }

    private static UdpClient CreateDualStackListener(int port)
    {
        // Mirror the client's strategy: prefer a single socket that accepts both IPv4 and IPv6.
        // Some platforms don't allow DualMode (e.g. older Linux with IPv6 disabled); fall back to
        // an IPv4-only listener when that happens.
        try
        {
            var udp = new UdpClient(AddressFamily.InterNetworkV6);
            udp.Client.DualMode = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
            return udp;
        }
        catch (SocketException)
        {
            return new UdpClient(new IPEndPoint(IPAddress.Any, port));
        }
        catch (NotSupportedException)
        {
            return new UdpClient(new IPEndPoint(IPAddress.Any, port));
        }
    }

    private async Task ProcessDatagramAsync(
        UdpClient udp, UdpReceiveResult datagram, CancellationToken ct)
    {
        Interlocked.Increment(ref _inFlightProcessing);
        try
        {
            try
            {
                await _concurrency.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Decrement(ref _pendingRequests);
                return;
            }

            try
            {
                Interlocked.Decrement(ref _pendingRequests);
                await ProcessDatagramCoreAsync(udp, datagram, ct).ConfigureAwait(false);
            }
            // The receive loop discards the Task returned by this method (fire-and-forget); any
            // exception we let escape becomes an unobserved-task fault, which is a host-stability
            // hazard in production. Catch everything here, log it, and swallow. Cancellation on
            // shutdown is normal and logged at Debug.
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogDebug("Request processing cancelled during shutdown for {Endpoint}",
                    datagram.RemoteEndPoint);
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled exception while processing CoAP datagram from {Endpoint}",
                    datagram.RemoteEndPoint);
                await InvokeDatagramExceptionFiltersAsync(datagram, ex, ct).ConfigureAwait(false);
            }
            finally
            {
                _concurrency.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _inFlightProcessing);
        }
    }

    private async Task ProcessDatagramCoreAsync(
        UdpClient udp, UdpReceiveResult datagram, CancellationToken ct)
    {
        CoapMessage message;
        try
        {
            message = CoapMessage.Parse(datagram.Buffer, _options.ParseLimits);
        }
        catch (FormatException ex)
        {
            _logger.LogDebug(ex, "Malformed CoAP message from {Endpoint}",
                datagram.RemoteEndPoint);
            return;
        }

        if (message.Type == CoapMessageType.Acknowledgement)
            return;

        if (message.Type == CoapMessageType.Reset)
        {
            if (_observers.Deregister(datagram.RemoteEndPoint, message.Token))
            {
                _logger.LogDebug("Observer deregistered via RST from {Endpoint}",
                    datagram.RemoteEndPoint);
            }
            return;
        }

        // RFC 7252, Section 4.5: short-circuit duplicate Confirmable requests so the handler runs
        // exactly once per (endpoint, MID) pair and retransmissions receive byte-identical
        // responses. NON requests are not deduplicated because they have no retransmission
        // semantics; clients that need at-most-once on NON should use CON.
        if (message.Type == CoapMessageType.Confirmable)
        {
            switch (_dedup.TryClaim(datagram.RemoteEndPoint, message.MessageId, out var cachedBytes))
            {
                case DedupOutcome.Replay:
                    await SendDatagramAsync(udp, cachedBytes!, datagram.RemoteEndPoint, ct).ConfigureAwait(false);
                    return;

                case DedupOutcome.Drop:
                    _logger.LogDebug(
                        "Duplicate CON from {Endpoint} (MID {MessageId}) dropped: handler still in flight",
                        datagram.RemoteEndPoint, message.MessageId);
                    return;
            }
        }

        try
        {
            await ProcessConfirmableOrNonAsync(udp, datagram, message, ct).ConfigureAwait(false);
        }
        finally
        {
            // ReleaseClaim is a no-op when the response bytes have already been recorded
            // (SetResponse populated CachedResponse). It only removes in-flight markers left
            // behind by paths that exited without sending a response (cancellation, route-not-
            // found that didn't reach SendResponseAsync, etc.), so a future retransmission can
            // re-enter the handler instead of being silently dropped until the TTL elapses.
            if (message.Type == CoapMessageType.Confirmable)
                _dedup.ReleaseClaim(datagram.RemoteEndPoint, message.MessageId);
        }
    }

    private async Task ProcessConfirmableOrNonAsync(
        UdpClient udp, UdpReceiveResult datagram, CoapMessage message, CancellationToken ct)
    {
        CoapBlockOption? block1Echo = null;

        if (message.Block1 is not null && _options.Block1MaxPayloadBytes > 0)
        {
            var outcome = _coordinator.OnBlock1Received(message, datagram.RemoteEndPoint);
            if (outcome.ImmediateResponse is { } immediate)
            {
                await SendResponseAsync(
                    udp, datagram.RemoteEndPoint, message,
                    immediate,
                    block1Echo: null,
                    observeResponse: null,
                    size1: outcome.Size1Hint,
                    ct).ConfigureAwait(false);
                return;
            }

            message = outcome.Reassembled!;
            block1Echo = outcome.Block1Echo;
        }

        if (message.Block2 is { Number: > 0 } followUp
            && _options.ResponseBlockSize is not null
            && _coordinator.TryServeBlock2FromCache(
                datagram.RemoteEndPoint,
                (CoapMethod)message.Code,
                message.UriPath,
                message.UriQuery,
                message.Token,
                followUp,
                out var cachedResponse))
        {
            await SendResponseAsync(
                udp, datagram.RemoteEndPoint, message,
                cachedResponse,
                block1Echo: null,
                observeResponse: null,
                size1: null,
                ct).ConfigureAwait(false);
            return;
        }

        var request = new CoapRequest(message, datagram.RemoteEndPoint);
        CoapResponse response;

        try
        {
            var route = FindRoute((CoapMethod)message.Code, request.Path);
            response = route is null
                ? CoapResponse.NotFound()
                : await route.Handler.HandleAsync(request, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling CoAP request on {Path}", request.Path);
            response = await InvokeRequestExceptionFiltersAsync(request, ex, ct).ConfigureAwait(false);
        }

        int? observeResponse = null;
        if (request.Observe == CoapConstants.ObserveRegister
            && ShouldRegisterObserver(response.Code))
        {
            _observers.Register(datagram.RemoteEndPoint, message.Token, request.Path);
            observeResponse = 1;
            _logger.LogDebug("Observer registered from {Endpoint} for {Path}",
                datagram.RemoteEndPoint, request.Path);
        }
        else if (request.Observe == CoapConstants.ObserveDeregister)
        {
            _observers.Deregister(datagram.RemoteEndPoint, message.Token);
            _logger.LogDebug("Observer deregistered from {Endpoint}",
                datagram.RemoteEndPoint);
        }

        // Transparent Block2 splitting: skip for Observe-registration responses (RFC 7959, Section 3.4
        // interaction is a documented non-goal).
        if (observeResponse is null)
        {
            response = _coordinator.ApplyBlock2Response(
                datagram.RemoteEndPoint,
                (CoapMethod)message.Code,
                message.UriPath,
                message.UriQuery,
                message.Token,
                message.Block2,
                response);
        }

        await SendResponseAsync(
            udp, datagram.RemoteEndPoint, message,
            response,
            block1Echo,
            observeResponse,
            size1: null,
            ct).ConfigureAwait(false);
    }

    private Task SendResponseAsync(
        UdpClient udp,
        IPEndPoint endpoint,
        CoapMessage request,
        CoapResponse response,
        CoapBlockOption? block1Echo,
        int? observeResponse,
        int? size1,
        CancellationToken ct)
    {
        var responseType = request.Type == CoapMessageType.Confirmable
            ? CoapMessageType.Acknowledgement
            : CoapMessageType.NonConfirmable;

        var bytes = CoapMessage.BuildResponse(
            responseType, response.Code, request.MessageId,
            request.Token, response.ContentFormat, response.Payload,
            observe: observeResponse,
            block2: response.Block2,
            block1: block1Echo ?? response.Block1,
            size1: size1);

        // Cache the exact bytes we are about to put on the wire so RFC 7252, Section 4.5
        // retransmissions receive a byte-identical reply.
        if (request.Type == CoapMessageType.Confirmable)
            _dedup.SetResponse(endpoint, request.MessageId, bytes);

        return SendDatagramAsync(udp, bytes, endpoint, ct);
    }

    private async Task RunNotifierAsync(UdpClient udp, CancellationToken ct)
    {
        try
        {
            await foreach (var path in _notifyChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                var observers = _observers.GetObservers(path);
                if (observers.Count == 0)
                    continue;

                var route = FindRoute(CoapMethod.Get, path);
                if (route is null)
                    continue;

                foreach (var observer in observers)
                {
                    try
                    {
                        await SendNotificationAsync(
                            udp, observer, route.Handler, path, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to send Observe notification to {Endpoint} for {Path}",
                            observer.RemoteEndpoint, path);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SendNotificationAsync(
        UdpClient udp,
        ObserverEntry observer,
        ICoapRequestHandler handler,
        string path,
        CancellationToken ct)
    {
        // Synthesise a CoapRequest for the handler. The MessageId/token on the fake message are
        // NOT used on the wire - they exist only so the handler sees a well-formed request.
        var tokenCopy = observer.TokenBytes;
        var fakeMessage = new CoapMessage
        {
            Version = CoapConstants.Version,
            Type = CoapMessageType.NonConfirmable,
            Code = CoapCode.Get,
            MessageId = NewMessageId(),
            Token = tokenCopy,
            Options = BuildUriPathOptions(path),
            Payload = [],
        };

        var fakeRequest = new CoapRequest(fakeMessage, observer.RemoteEndpoint);
        var result = await handler.HandleAsync(fakeRequest, ct).ConfigureAwait(false);
        if (result.Payload is null)
        {
            _logger.LogDebug(
                "Handler returned null payload for Observe notification to {Endpoint} ({Path}); skipped",
                observer.RemoteEndpoint, path);
            return;
        }

        var seqNum = observer.NextSequenceNumber();
        var bytes = CoapMessage.BuildResponse(
            CoapMessageType.NonConfirmable,
            result.Code,
            NewMessageId(),
            tokenCopy,
            result.ContentFormat,
            result.Payload,
            observe: seqNum);

        if (!await SendDatagramAsync(udp, bytes, observer.RemoteEndpoint, ct).ConfigureAwait(false))
            return;

        _logger.LogTrace("Sent Observe notification #{Seq} to {Endpoint} for {Path}",
            seqNum, observer.RemoteEndpoint, path);
    }

    private async Task SendServiceUnavailableAsync(
        UdpClient udp, UdpReceiveResult datagram, CancellationToken ct)
    {
        try
        {
            var message = CoapMessage.Parse(datagram.Buffer, _options.ParseLimits);
            if (message.Type != CoapMessageType.Confirmable)
                return;

            var bytes = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.ServiceUnavailable,
                message.MessageId,
                message.Token,
                contentFormat: null,
                payload: null);

            // Back-pressure rejections are deliberately not entered into the dedup table: the
            // path that builds them runs before any TryClaim, and re-rejecting a retransmission
            // is cheap, deterministic, and does not mutate any shared state.

            await SendDatagramAsync(udp, bytes, datagram.RemoteEndPoint, ct).ConfigureAwait(false);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex,
                "Best-effort 5.03 rejection failed for {Endpoint}", datagram.RemoteEndPoint);
        }
    }

    private async Task<bool> SendDatagramAsync(
        UdpClient udp, byte[] bytes, IPEndPoint endpoint, CancellationToken ct)
    {
        try
        {
            await udp.SendAsync(bytes, endpoint, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SocketException ex)
        {
            _logger.LogDebug(ex, "Failed to send CoAP datagram to {Endpoint}", endpoint);
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private CoapServer MapMethod(
        CoapMethod method, string pattern,
        Func<CoapRequest, CancellationToken, Task<CoapResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(handler);
        EnsureNotStarted();
        _routes.Add(new RouteEntry(method, pattern, new DelegateHandler(handler)));
        return this;
    }

    private void EnsureNotStarted()
    {
        if (Volatile.Read(ref _started) != 0)
            throw new InvalidOperationException(
                "Routes must be registered before the CoAP server is started.");
    }

    private RouteEntry? FindRoute(CoapMethod method, string path)
    {
        foreach (var route in _routes)
        {
            if (route.Method.HasValue && route.Method.Value != method)
                continue;
            if (CoapPathMatcher.IsMatch(route.Pattern, path))
                return route;
        }
        return null;
    }

    private static ushort NewMessageId()
        => (ushort)Random.Shared.Next(0, ushort.MaxValue + 1);

    private async Task<CoapResponse> InvokeRequestExceptionFiltersAsync(
        CoapRequest request, Exception exception, CancellationToken ct)
    {
        if (_requestExceptionFilters.Count == 0)
            return CoapResponse.InternalError("Internal Server Error");

        var context = new CoapRequestExceptionContext(request, exception);
        foreach (var filter in _requestExceptionFilters)
        {
            CoapResponse? response;
            try
            {
                response = await filter.OnExceptionAsync(context, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception filterEx)
            {
                // A faulty filter must never crash the server, mask the original exception in the
                // logs, or propagate to other filters. Log and skip; the next filter (or the
                // default 5.00) takes over.
                _logger.LogError(filterEx,
                    "Request exception filter {Filter} threw while handling {Original} on {Path}",
                    filter.GetType().FullName, exception.GetType().FullName, request.Path);
                continue;
            }

            if (response is not null)
                return response;
        }

        return CoapResponse.InternalError("Internal Server Error");
    }

    internal async Task InvokeDatagramExceptionFiltersAsync(
        UdpReceiveResult datagram, Exception exception, CancellationToken ct)
    {
        if (_datagramExceptionFilters.Count == 0)
            return;

        var context = new CoapDatagramExceptionContext(
            datagram.RemoteEndPoint, datagram.Buffer, exception);

        foreach (var filter in _datagramExceptionFilters)
        {
            try
            {
                await filter.OnExceptionAsync(context, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
            catch (Exception filterEx)
            {
                _logger.LogError(filterEx,
                    "Datagram exception filter {Filter} threw while handling {Original} from {Endpoint}",
                    filter.GetType().FullName, exception.GetType().FullName, datagram.RemoteEndPoint);
            }
        }
    }

    /// <summary>
    /// Decides whether a successful response to an Observe-registration request should actually
    /// register the observer. Honours <see cref="CoapServerOptions.ObserveRegistrationPredicate"/>
    /// when set; otherwise registers for any code in the splittable-success band
    /// (<c>2.01 Created</c> .. <c>2.05 Content</c>), matching the band used by transparent Block2.
    /// </summary>
    private bool ShouldRegisterObserver(byte responseCode)
    {
        var predicate = _options.ObserveRegistrationPredicate;
        if (predicate is not null)
            return predicate(responseCode);

        return responseCode >= CoapCode.Created && responseCode <= CoapCode.Content;
    }

    private static List<CoapOption> BuildUriPathOptions(string path)
    {
        // Hand-rolled segment walk to avoid the Split-array + LINQ-enumerator allocations on
        // every Observe notification. UTF-8 encode each segment into its own option.
        var options = new List<CoapOption>(capacity: 4);
        int start = 0;
        for (int i=0; i <= path.Length; ++i)
        {
            if (i == path.Length || path[i] == '/')
            {
                int len = i - start;
                if (len > 0)
                {
                    var bytes = Encoding.UTF8.GetBytes(path, start, len);
                    options.Add(new CoapOption(CoapOptionNumber.UriPath, bytes));
                }
                start = i + 1;
            }
        }
        return options;
    }

    private sealed record RouteEntry(CoapMethod? Method, string Pattern, ICoapRequestHandler Handler);

    private sealed class DelegateHandler(
        Func<CoapRequest, CancellationToken, Task<CoapResponse>> handler)
        : ICoapRequestHandler
    {
        public Task<CoapResponse> HandleAsync(CoapRequest request, CancellationToken ct) =>
            handler(request, ct);
    }
}