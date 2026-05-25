using System.Net;

namespace Tinkwell.Coap.Server;

/// <summary>
/// Represents an incoming CoAP request presented to a handler (RFC 7252, Section 5).
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="CoapRequest"/> wraps the parsed <see cref="CoapMessage"/> and the remote endpoint
/// that sent it, exposing only the fields relevant to application handlers. The instance is
/// constructed by the server after routing succeeds and is effectively immutable: handlers
/// should treat all properties (including collections) as read-only snapshots.
/// </para>
/// <para>Example:</para>
/// <code>
/// server.MapPut("/config/interval", (request, ct) =>
/// {
///     if (request.ContentFormat != CoapContentFormat.TextPlain)
///         return Task.FromResult(CoapResponse.BadRequest("expected text/plain"));
///
///     var value = System.Text.Encoding.UTF8.GetString(request.Payload.Span);
///     UpdateInterval(int.Parse(value));
///     return Task.FromResult(CoapResponse.Changed());
/// });
/// </code>
/// </remarks>
public sealed class CoapRequest
{
    private readonly byte[] _token;

    /// <summary>CoAP method of the request (e.g. <see cref="CoapMethod.Get"/>).</summary>
    /// <remarks>
    /// The underlying byte value matches the wire encoding from RFC 7252, Section 12.1.1. If the
    /// client sends a code that is not one of the standard methods the cast to
    /// <see cref="CoapMethod"/> may produce an undefined enum value; such requests never match a
    /// registered route and receive <c>4.04 Not Found</c>.
    /// </remarks>
    public CoapMethod Method { get; }

    /// <summary>URI path reconstructed from <c>Uri-Path</c> options (RFC 7252, Section 5.10.1).</summary>
    /// <value>Always begins with a forward slash, e.g. <c>"/sensors/temperature"</c>; never <see langword="null"/>.</value>
    public string Path { get; }

    /// <summary>URI query reconstructed from <c>Uri-Query</c> options, or <see langword="null"/> if absent.</summary>
    /// <value>The query string <i>without</i> the leading <c>?</c>, e.g. <c>"unit=celsius&amp;precision=1"</c>.</value>
    public string? Query { get; }

    /// <summary>Raw request payload bytes.</summary>
    /// <remarks>
    /// The payload may be empty (e.g. for most GET requests). The memory is backed by the parsed
    /// message buffer; do not store it beyond the lifetime of the handler invocation.
    /// </remarks>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>
    /// Content-Format of the request payload (RFC 7252, Section 5.10.3), or <see langword="null"/>
    /// when the client did not advertise one.
    /// </summary>
    public CoapContentFormat? ContentFormat { get; }

    /// <summary>
    /// Accept option values indicating the formats the client prefers for the response
    /// (RFC 7252, Section 5.10.4). Empty when the client did not advertise any preference.
    /// </summary>
    public IReadOnlyList<CoapContentFormat> AcceptFormats { get; }

    /// <summary>
    /// Observe option value (RFC 7641, Section 2), or <see langword="null"/> if absent.
    /// </summary>
    /// <remarks>
    /// In a client request this value is either <see cref="CoapConstants.ObserveRegister"/>
    /// (<c>0</c>) to register as an observer or <see cref="CoapConstants.ObserveDeregister"/>
    /// (<c>1</c>) to cancel the registration. The server registers the observer automatically
    /// when a <c>GET</c> with <c>Observe = 0</c> succeeds with <c>2.05 Content</c>.
    /// </remarks>
    public int? Observe { get; }

    /// <summary>Block1 option from the request, or <see langword="null"/> if absent (RFC 7959, Section 2.5).</summary>
    /// <remarks>
    /// When transparent Block1 reassembly is enabled (the default, see
    /// <see cref="CoapServerOptions.Block1MaxPayloadBytes"/>), the handler is invoked once with
    /// the complete payload and <see cref="Block1"/> reports the final chunk's option value
    /// (<c>NUM=last, M=false, SZX=...</c>). When reassembly is disabled this is the raw option
    /// from each individual chunk.
    /// </remarks>
    public CoapBlockOption? Block1 { get; }

    /// <summary>Block2 option from the request, or <see langword="null"/> if absent (RFC 7959, Section 2.5).</summary>
    /// <remarks>
    /// Inspect this when implementing Block2 behaviour manually. When transparent Block2
    /// splitting is enabled (the default, see <see cref="CoapServerOptions.ResponseBlockSize"/>),
    /// the server honours the client's requested block size and serves follow-up blocks from its
    /// cache without re-invoking the handler.
    /// </remarks>
    public CoapBlockOption? Block2 { get; }

    /// <summary>Remote endpoint that sent the request.</summary>
    public IPEndPoint RemoteEndpoint { get; }

    /// <summary>
    /// The CoAP token associated with the request (RFC 7252, Section 5.3.1), as a read-only view
    /// over the server's internal buffer.
    /// </summary>
    /// <remarks>
    /// Tokens are 0 to 8 bytes and are chosen by the client. Do not store the underlying array
    /// beyond the scope of the handler; call <see cref="ReadOnlyMemory{T}.ToArray"/> for a copy.
    /// </remarks>
    public ReadOnlyMemory<byte> Token => _token;

    /// <summary>
    /// All CoAP options from the request, including standard and custom option numbers.
    /// </summary>
    /// <remarks>
    /// Inspect this list to read vendor-specific or authentication-related options (e.g. a PSK
    /// token in a private option number). The list is a snapshot and does not change when the
    /// underlying message is re-parsed.
    /// </remarks>
    public IReadOnlyList<CoapOption> Options { get; }

    internal CoapRequest(CoapMessage message, IPEndPoint remoteEndpoint)
    {
        Method = (CoapMethod)message.Code;
        Path = message.UriPath;
        Query = message.UriQuery;
        Payload = message.Payload;
        ContentFormat = message.RequestContentFormat;
        AcceptFormats = message.AcceptFormats;
        Observe = message.Observe;
        Block1 = message.Block1;
        Block2 = message.Block2;
        _token = message.Token;
        RemoteEndpoint = remoteEndpoint;
        Options = message.Options.AsReadOnly();
    }
}
