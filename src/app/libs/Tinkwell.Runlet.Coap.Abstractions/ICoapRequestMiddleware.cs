namespace Tinkwell.Runlet.Coap;

/// <summary>
/// Cross-cutting middleware that runs around every CoAP request handler
/// (both <c>.tw</c>-configured bindings and code-defined routes).
/// Register in DI during <c>ConfigureServices</c>; the CoAP runlet
/// discovers and orders all middlewares at startup.
/// </summary>
/// <remarks>
/// Call <c>next</c> in <see cref="InvokeAsync"/> to continue
/// the pipeline, or return early to short-circuit (e.g. for auth checks).
/// </remarks>
public interface ICoapRequestMiddleware
{
    /// <summary>
    /// Processes the request. Call <paramref name="next"/> to invoke the
    /// inner handler; return a <see cref="CoapMiddlewareResult"/> to override
    /// the response, or <see langword="null"/> to let the inner result pass through.
    /// </summary>
    Task<CoapMiddlewareResult?> InvokeAsync(
        CoapRequestContext context,
        Func<CoapRequestContext, CancellationToken, Task<CoapMiddlewareResult?>> next,
        CancellationToken ct);

    /// <summary>
    /// Controls execution order. Lower values run first (outermost).
    /// Default is 0.
    /// </summary>
    int Order => 0;
}
