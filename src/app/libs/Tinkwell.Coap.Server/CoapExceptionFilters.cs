using System.Net;

namespace Tinkwell.Coap.Server;

/// <summary>
/// Information passed to an <see cref="ICoapRequestExceptionFilter"/> when a route handler throws.
/// </summary>
/// <remarks>
/// The instance is constructed by the server immediately after a route handler raises an
/// exception other than <see cref="OperationCanceledException"/> (which is treated as a silent
/// drop on shutdown). Filters receive a fully-populated <see cref="Request"/> and the
/// <see cref="Exception"/> instance as it was thrown - the server does not unwrap
/// <see cref="AggregateException"/> for you, so handlers that fan out work and rethrow should
/// flatten the exception themselves before letting it escape.
/// </remarks>
public sealed class CoapRequestExceptionContext
{
    internal CoapRequestExceptionContext(CoapRequest request, Exception exception)
    {
        Request = request;
        Exception = exception;
    }

    /// <summary>The request that was being processed when the handler threw.</summary>
    public CoapRequest Request { get; }

    /// <summary>The exception raised by the route handler.</summary>
    public Exception Exception { get; }
}

/// <summary>
/// Information passed to an <see cref="ICoapDatagramExceptionFilter"/> when the datagram pipeline
/// itself faults outside the route handler (for example, blockwise reassembly or transport sends).
/// </summary>
/// <remarks>
/// <para>
/// The server may not have a parsed <see cref="CoapMessage"/> at this point - parse failures are
/// caught earlier and converted into silent drops - so filters operate on the raw datagram bytes
/// and the remote endpoint that sent them. CoAP also offers no defined response for this scope:
/// the server has no usable Message ID/token to build an ACK once the parse step has been bypassed
/// or already failed. Filters here are observer-style hooks intended for diagnostics, structured
/// logging, or external sinks (Sentry, Application Insights, custom metrics).
/// </para>
/// </remarks>
public sealed class CoapDatagramExceptionContext
{
    internal CoapDatagramExceptionContext(
        IPEndPoint remoteEndpoint, ReadOnlyMemory<byte> datagram, Exception exception)
    {
        RemoteEndpoint = remoteEndpoint;
        Datagram = datagram;
        Exception = exception;
    }

    /// <summary>Remote endpoint that sent the datagram whose processing faulted.</summary>
    public IPEndPoint RemoteEndpoint { get; }

    /// <summary>Raw bytes of the datagram, exactly as received from the OS.</summary>
    /// <remarks>
    /// The memory is backed by the receive buffer for the lifetime of the filter invocation only;
    /// if you need to retain the bytes (for example to push them to a log sink), call
    /// <see cref="ReadOnlyMemory{T}.ToArray"/> for a defensive copy.
    /// </remarks>
    public ReadOnlyMemory<byte> Datagram { get; }

    /// <summary>The exception raised by the datagram pipeline.</summary>
    public Exception Exception { get; }
}

/// <summary>
/// Filter invoked when a route handler raises an exception, with the option to override the
/// default <c>5.00 Internal Server Error</c> response.
/// </summary>
/// <remarks>
/// <para>
/// Register filters with
/// <see cref="CoapServer.UseRequestExceptionFilter(ICoapRequestExceptionFilter)"/> before the
/// server starts. Filters run in registration order; the first filter that returns a non-<see langword="null"/>
/// <see cref="CoapResponse"/> wins and its response is sent to the client (the remaining filters
/// are not invoked). When every filter returns <see langword="null"/>, or no filters are
/// registered, the server falls back to <see cref="CoapResponse.InternalError(string)"/>.
/// </para>
/// <para>
/// A filter that itself throws is logged and skipped; the exception does not propagate, does not
/// alter the response chosen by other filters, and does not crash the server.
/// </para>
/// <para>Example:</para>
/// <code>
/// public sealed class NotFoundFilter : ICoapRequestExceptionFilter
/// {
///     public Task&lt;CoapResponse?&gt; OnExceptionAsync(
///         CoapRequestExceptionContext context, CancellationToken ct)
///     {
///         if (context.Exception is KeyNotFoundException)
///             return Task.FromResult&lt;CoapResponse?&gt;(CoapResponse.NotFound());
///         return Task.FromResult&lt;CoapResponse?&gt;(null);
///     }
/// }
///
/// server.UseRequestExceptionFilter(new NotFoundFilter());
/// </code>
/// </remarks>
public interface ICoapRequestExceptionFilter
{
    /// <summary>Inspects the failure and optionally produces a response to send back to the client.</summary>
    /// <param name="context">Exception context; never <see langword="null"/>.</param>
    /// <param name="ct">
    /// Cancellation token linked to the host's stopping token - signalled when the server is
    /// shutting down.
    /// </param>
    /// <returns>
    /// A response to send to the client, or <see langword="null"/> to defer to the next filter (or
    /// the default <c>5.00 Internal Server Error</c> when no filter handles the exception).
    /// </returns>
    /// <example>
    /// <para>Return <c>4.04</c> for a missing domain entity, otherwise let the default <c>5.00</c> win.</para>
    /// <code>
    /// public Task&lt;CoapResponse?&gt; OnExceptionAsync(
    ///     CoapRequestExceptionContext context, CancellationToken ct)
    /// {
    ///     if (context.Exception is FileNotFoundException)
    ///         return Task.FromResult&lt;CoapResponse?&gt;(CoapResponse.NotFound());
    ///     return Task.FromResult&lt;CoapResponse?&gt;(null);
    /// }
    /// </code>
    /// </example>
    Task<CoapResponse?> OnExceptionAsync(
        CoapRequestExceptionContext context, CancellationToken ct);
}

/// <summary>
/// Filter invoked when the datagram pipeline faults outside a route handler.
/// </summary>
/// <remarks>
/// <para>
/// Register filters with
/// <see cref="CoapServer.UseDatagramExceptionFilter(ICoapDatagramExceptionFilter)"/> before the
/// server starts. All registered filters are invoked in registration order (observer fan-out); a
/// filter that throws is logged and skipped, but does not prevent the remaining filters from
/// running. The server's existing error log line continues to fire whether or not any filters are
/// registered, so default behaviour is preserved.
/// </para>
/// <para>
/// CoAP does not define a response for malformed or pipeline-faulted datagrams, so this hook does
/// not produce a <see cref="CoapResponse"/>. Use it to observe and report; if you need to
/// influence the response sent to the client, use
/// <see cref="ICoapRequestExceptionFilter"/> instead.
/// </para>
/// </remarks>
public interface ICoapDatagramExceptionFilter
{
    /// <summary>Observes a datagram-pipeline fault.</summary>
    /// <param name="context">Datagram fault context; never <see langword="null"/>.</param>
    /// <param name="ct">
    /// Cancellation token linked to the host's stopping token - signalled when the server is
    /// shutting down.
    /// </param>
    /// <returns>A task that completes when the filter has finished its work.</returns>
    /// <example>
    /// <para>Attach diagnostics when blockwise reassembly or UDP send throws before routing.</para>
    /// <code>
    /// public Task OnExceptionAsync(
    ///     CoapDatagramExceptionContext context, CancellationToken ct)
    /// {
    ///     logger.LogError(
    ///         context.Exception, "CoAP datagram fault from {Endpoint}", context.RemoteEndpoint);
    ///     return Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task OnExceptionAsync(
        CoapDatagramExceptionContext context, CancellationToken ct);
}

internal sealed class DelegateRequestExceptionFilter(
    Func<CoapRequestExceptionContext, CancellationToken, Task<CoapResponse?>> handler)
    : ICoapRequestExceptionFilter
{
    public Task<CoapResponse?> OnExceptionAsync(
        CoapRequestExceptionContext context, CancellationToken ct) =>
        handler(context, ct);
}

internal sealed class DelegateDatagramExceptionFilter(
    Func<CoapDatagramExceptionContext, CancellationToken, Task> handler)
    : ICoapDatagramExceptionFilter
{
    public Task OnExceptionAsync(
        CoapDatagramExceptionContext context, CancellationToken ct) =>
        handler(context, ct);
}
