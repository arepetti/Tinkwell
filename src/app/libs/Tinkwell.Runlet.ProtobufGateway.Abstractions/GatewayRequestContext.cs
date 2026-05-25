using Tinkwell.Coap.Server;

namespace Tinkwell.Runlet.ProtobufGateway;

/// <summary>
/// Request-scoped context passed through the gateway middleware pipeline.
/// Contains the extracted service/method names, the raw CoAP request,
/// and a mutable items bag for middleware to pass data downstream.
/// </summary>
public sealed class GatewayRequestContext
{
    /// <summary>Full proto service name extracted from the path (e.g. <c>"tinkwell.measures.v1.Measures"</c>).</summary>
    public required string Service { get; init; }

    /// <summary>RPC method name extracted from the path (e.g. <c>"Update"</c>).</summary>
    public required string Method { get; init; }

    /// <summary>
    /// Name of the <c>protobuf-gateway</c> profile that matched this route,
    /// or a comma-separated list if multiple profiles were merged.
    /// </summary>
    public required string ProfileName { get; init; }

    /// <summary>The underlying CoAP request.</summary>
    public required CoapRequest Request { get; init; }

    /// <summary>
    /// Mutable property bag for middleware to attach data that downstream
    /// middleware or the tunnel handler can consume (e.g. authenticated
    /// device identity, tenant ID).
    /// </summary>
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
