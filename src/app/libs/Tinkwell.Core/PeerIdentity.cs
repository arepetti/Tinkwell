using System.Net;

namespace Tinkwell;

/// <summary>
/// Typed record representing what is known about the sender of a request.
/// Populated by the transport layer; fields are null when the transport
/// does not provide the information.
/// </summary>
/// <remarks>
/// When DTLS is added, <see cref="TlsIdentity"/> will carry the PSK
/// identity or certificate CN established during the handshake.
/// </remarks>
/// <param name="Endpoint">Remote IP address and port of the sender.</param>
/// <param name="TlsIdentity">
/// DTLS PSK identity or certificate CN. Null until DTLS transport is implemented.
/// </param>
public sealed record PeerIdentity(
    IPEndPoint Endpoint,
    string? TlsIdentity = null);
