namespace Tinkwell.Coap.Server;

/// <summary>
/// Handles CoAP requests for a specific resource or set of resources.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface when a single handler needs to respond to multiple methods on the
/// same path (for example a resource that supports both <c>GET</c> and <c>PUT</c>). For simple
/// per-method handlers, prefer the lambda-based helpers such as
/// <see cref="CoapServer.MapGet(string,Func{CoapRequest,CancellationToken,Task{CoapResponse}})"/>.
/// </para>
/// <para>Example:</para>
/// <code>
/// public sealed class SensorHandler : ICoapRequestHandler
/// {
///     public Task&lt;CoapResponse&gt; HandleAsync(CoapRequest request, CancellationToken ct)
///         => request.Method switch
///         {
///             CoapMethod.Get => HandleGet(request),
///             CoapMethod.Put => HandlePut(request),
///             _              => Task.FromResult(CoapResponse.MethodNotAllowed()),
///         };
/// }
///
/// server.Map("/sensors/+", new SensorHandler());
/// </code>
/// </remarks>
public interface ICoapRequestHandler
{
    /// <summary>Processes an incoming CoAP request and produces a response.</summary>
    /// <param name="request">The parsed CoAP request; never <see langword="null"/>.</param>
    /// <param name="ct">
    /// Cancellation token linked to the host's stopping token - signalled when the server is
    /// shutting down. Handlers should respect cancellation and throw
    /// <see cref="OperationCanceledException"/> promptly; the server converts that into a silent
    /// response drop.
    /// </param>
    /// <returns>A task that resolves to the response to send back to the client.</returns>
    /// <example>
    /// <para>Class-based resource that branches on the wire method and path segments.</para>
    /// <code>
    /// public Task&lt;CoapResponse&gt; HandleAsync(CoapRequest request, CancellationToken ct) =>
    ///     request.Method switch
    ///     {
    ///         CoapMethod.Get => Task.FromResult(
    ///             CoapResponse.Content(currentJson, CoapContentFormat.ApplicationJson)),
    ///         CoapMethod.Put => SaveAsync(request, ct),
    ///         _ =&gt; Task.FromResult(CoapResponse.MethodNotAllowed()),
    ///     };
    /// </code>
    /// </example>
    Task<CoapResponse> HandleAsync(CoapRequest request, CancellationToken ct);
}
