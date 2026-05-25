using Tinkwell.Coap;

namespace Tinkwell.Runlet.Coap;

/// <summary>
/// Request-scoped context passed through the CoAP middleware pipeline.
/// Contains the URI components, payload, sender identity, and a mutable
/// items bag for middleware to pass data downstream.
/// </summary>
public sealed class CoapRequestContext
{
    /// <summary>URI path of the CoAP request (e.g. <c>/sensor/temperature</c>).</summary>
    public required string Path { get; init; }

    /// <summary>Query string if present, or <see langword="null"/>.</summary>
    public string? Query { get; init; }

    /// <summary>Request body as a UTF-8 string, or <see langword="null"/> for GET.</summary>
    public string? Payload { get; init; }

    /// <summary>Protocol method: <c>GET</c>, <c>POST</c>, <c>PUT</c>, <c>DELETE</c>.</summary>
    public required string Method { get; init; }

    /// <summary>Raw binary payload, or <see langword="null"/> when the payload is text-only.</summary>
    public byte[]? PayloadBytes { get; init; }

    /// <summary>
    /// Content-Format of the request payload (RFC 7252, Section 5.10.3).
    /// Null when not provided or not applicable.
    /// </summary>
    public CoapContentFormat? RequestContentFormat { get; init; }

    /// <summary>
    /// Identity of the peer that sent the request. Populated by the CoAP
    /// transport; <see langword="null"/> when DTLS is not in use.
    /// </summary>
    public PeerIdentity? Peer { get; init; }

    /// <summary>
    /// Mutable property bag for middleware to attach data that downstream
    /// middleware or handlers can consume (e.g. authenticated device identity,
    /// tenant ID).
    /// </summary>
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
