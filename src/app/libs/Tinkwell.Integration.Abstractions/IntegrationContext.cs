using Tinkwell;
using Tinkwell.Coap;

namespace Tinkwell.Integration;

/// <summary>
/// Captures the request-scoped data available to all bindings during
/// a single inbound message or request. Protocol-specific runlets
/// populate the relevant fields; unused fields are <see langword="null"/>.
/// </summary>
public sealed class IntegrationContext
{
    /// <summary>
    /// URI path or topic (e.g. <c>/sensor/temperature</c> for CoAP,
    /// <c>sensor/temperature</c> for MQTT).
    /// </summary>
    public string Path { get; }

    /// <summary>Query string if present (CoAP only).</summary>
    public string? Query { get; }

    /// <summary>Request/message body as a string, or empty for GET.</summary>
    public string? Payload { get; }

    /// <summary>
    /// Protocol method: <c>GET</c>, <c>POST</c>, <c>PUT</c>, <c>DELETE</c>
    /// for CoAP; <c>MESSAGE</c> for MQTT.
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// Initializes a new <see cref="IntegrationContext"/> for a single inbound request or message.
    /// </summary>
    /// <param name="path">URI path or topic.</param>
    /// <param name="query">Query string if present (CoAP only).</param>
    /// <param name="payload">Request/message body as a string, or <see langword="null"/> for GET.</param>
    /// <param name="method">Protocol method (e.g. <c>GET</c>, <c>PUT</c>, <c>MESSAGE</c>).</param>
    public IntegrationContext(string path, string? query, string? payload, string method)
    {
        Path = path;
        Query = query;
        Payload = payload;
        Method = method;
    }
    /// <summary>
    /// Raw payload bytes. Available for protocols that carry binary data
    /// (e.g. LwM2M TLV, SenML-CBOR). Null when the payload is text-only.
    /// </summary>
    public byte[]? PayloadBytes { get; init; }

    /// <summary>
    /// Content-Format of the request payload (RFC 7252, Section 5.10.3).
    /// Null when not provided or not applicable.
    /// </summary>
    public CoapContentFormat? RequestContentFormat { get; init; }

    /// <summary>
    /// Identity of the peer that sent the request. Populated by CoAP-based
    /// transports; null for MQTT (sender identity is in User Properties or
    /// the topic). When DTLS is enabled, <see cref="PeerIdentity.TlsIdentity"/>
    /// carries the PSK identity or certificate CN.
    /// </summary>
    public PeerIdentity? Peer { get; init; }

    /// <summary>
    /// Mutable property bag for middleware to pass resolved identity or
    /// other cross-cutting data downstream to bindings. Middleware may
    /// set e.g. <c>Items["device-id"]</c> after validating a token.
    /// </summary>
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// Builds the expression parameter dictionary that bindings pass
    /// to <see cref="Tinkwell.Expressions.IExpressionEvaluator"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object?> ToExpressionParameters() =>
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["path"] = Path,
            ["topic"] = Path,
            ["query"] = Query ?? string.Empty,
            ["payload"] = Payload ?? string.Empty,
            ["method"] = Method,
            ["peer_ip"] = Peer?.Endpoint.Address.ToString() ?? string.Empty,
            ["peer_identity"] = Peer?.TlsIdentity ?? string.Empty,
        };
}
