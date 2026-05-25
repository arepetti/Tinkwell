using Tinkwell.Coap.Server;

namespace Tinkwell.Runlet.ProtobufGateway;

/// <summary>
/// Cross-cutting middleware for the protobuf gateway pipeline. Runs after
/// path extraction and whitelist checks but before the gRPC tunnel call.
/// Register implementations in DI during <c>ConfigureServices</c>; the
/// gateway worker discovers and orders them at startup.
/// </summary>
/// <remarks>
/// <para>
/// Call <c>next</c> in <see cref="InvokeAsync"/> to continue
/// the pipeline, or return a <see cref="CoapResponse"/> directly to
/// short-circuit (e.g. for device-level access control).
/// </para>
/// <para>
/// Use <see cref="GatewayRequestContext.Items"/> to pass data between
/// middlewares (e.g. an authenticated device identity resolved by an
/// earlier middleware).
/// </para>
/// </remarks>
public interface IGatewayMiddleware
{
    /// <summary>
    /// Processes the gateway request. Call <paramref name="next"/> to invoke
    /// the next middleware (or the gRPC tunnel); return a
    /// <see cref="CoapResponse"/> to override the response.
    /// </summary>
    Task<CoapResponse> InvokeAsync(
        GatewayRequestContext context,
        Func<GatewayRequestContext, CancellationToken, Task<CoapResponse>> next,
        CancellationToken ct);

    /// <summary>
    /// Controls execution order. Lower values run first (outermost).
    /// Default is 0.
    /// </summary>
    int Order => 0;
}
