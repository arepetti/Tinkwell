using System.Text;

namespace Tinkwell.Coap;

/// <summary>
/// Describes a single CoAP request to be sent with <see cref="CoapClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// This class is a pure data holder. The URI (host, port, path, query) is passed separately to
/// <see cref="CoapClient.SendAsync(Uri, CoapClientRequest, CoapClientRequestOptions, CancellationToken)"/>
/// so that a single <c>CoapClientRequest</c> can be re-targeted at multiple endpoints.
/// </para>
/// <para>
/// All properties are optional. <see cref="Payload"/> defaults to <see langword="null"/> (no body)
/// and is mostly relevant for POST/PUT; GET and DELETE should leave it as <see langword="null"/>
/// or set it to an empty array. <see cref="MessageId"/> and <see cref="Token"/> are usually left
/// unset so that the client picks sensible random values.
/// </para>
/// <para>
/// <see cref="Token"/> and <see cref="Payload"/> are defensive-copied when set. Arrays returned from
/// those property getters reference the library's internal storage; callers must not mutate them.
/// </para>
/// <example>
/// <code>
/// var request = new CoapClientRequest("{\"temp\":21.5}", CoapContentFormat.ApplicationJson)
/// {
///     Method = CoapMethod.Post,
///     Accept = CoapContentFormat.ApplicationJson,
/// };
/// var response = await CoapClient.SendAsync(
///     new Uri("coap://device.local/sensors/temperature"),
///     request,
///     new CoapClientRequestOptions(),
///     cancellationToken);
/// </code>
/// </example>
/// </remarks>
public sealed class CoapClientRequest
{
    /// <summary>Creates an empty request. Populate <see cref="Method"/>, <see cref="Payload"/>, etc. before sending.</summary>
    public CoapClientRequest()
    {
    }

    /// <summary>
    /// Creates a request with a binary payload.
    /// </summary>
    /// <param name="payload">Request body bytes. Use an empty array for requests without a body.</param>
    /// <param name="contentFormat">Content-Format of <paramref name="payload"/>, or <see langword="null"/> to omit the option.</param>
    public CoapClientRequest(byte[] payload, CoapContentFormat? contentFormat = null)
    {
        Payload = payload;
        ContentFormat = contentFormat;
    }

    /// <summary>
    /// Creates a request with a UTF-8 text payload.
    /// </summary>
    /// <remarks>
    /// The second parameter defaults to <see cref="CoapContentFormat.TextPlain"/>. A JSON string body
    /// still requires <see cref="CoapContentFormat.ApplicationJson"/> (or another registered format such as
    /// <see cref="CoapContentFormat.ApplicationLwm2mJson"/>) or the Content-Format option on the wire will not match the payload.
    /// </remarks>
    /// <param name="payload">Payload to be UTF-8 encoded.</param>
    /// <param name="contentFormat">Content-Format; defaults to <see cref="CoapContentFormat.TextPlain"/>.</param>
    public CoapClientRequest(string payload, CoapContentFormat? contentFormat = CoapContentFormat.TextPlain)
        : this(Encoding.UTF8.GetBytes(payload), contentFormat)
    {
    }

    /// <summary>
    /// CoAP Message ID for the first wire message of the request (RFC 7252, Section 3).
    /// <para>
    /// Per RFC 7252, Section 4.4, every Confirmable CoAP message requires a unique Message ID within
    /// the exchange lifetime. During blockwise transfers (RFC 7959) each block exchange is a separate
    /// Confirmable message, so only the first block can use this value; the remaining blocks always
    /// get freshly generated Message IDs.
    /// </para>
    /// <para>
    /// When <see langword="null"/> (the default), a random Message ID is chosen.
    /// </para>
    /// </summary>
    public ushort? MessageId { get; init; }

    /// <summary>
    /// CoAP Token used to correlate the request with its response (RFC 7252, Section 5.3.1).
    /// <para>
    /// The client picks a Token that is unlikely to be in flight on the same endpoint at the same
    /// time. During blockwise transfers (RFC 7959, Section 2.4) the same Token is reused across
    /// every block exchange of a single <c>SendAsync</c> call, so that servers and intermediaries
    /// can treat the whole transfer as one logical request/response pair.
    /// </para>
    /// <para>
    /// When <see langword="null"/> (the default), a random 2-byte Token is generated once per
    /// <c>SendAsync</c> call and reused across all blocks of that call.
    /// </para>
    /// <para>
    /// The assigned value is copied; the getter returns the library's internal array, which must not
    /// be mutated by callers.
    /// </para>
    /// </summary>
    public byte[]? Token
    {
        get;
        init => field = value is null ? null : (byte[])value.Clone();
    }

    /// <summary>
    /// CoAP request method (RFC 7252, Section 5.8). Defaults to <see cref="CoapMethod.Get"/>.
    /// </summary>
    public CoapMethod Method { get; init; } = CoapMethod.Get;

    /// <summary>
    /// Content-Format option describing <see cref="Payload"/> (RFC 7252, Section 5.10.3).
    /// Leave <see langword="null"/> when no payload is sent or its format is unspecified.
    /// </summary>
    public CoapContentFormat? ContentFormat { get; init; }

    /// <summary>
    /// Accept option advertising the preferred response Content-Format (RFC 7252, Section 5.10.4).
    /// Leave <see langword="null"/> to not constrain the server's choice.
    /// </summary>
    public CoapContentFormat? Accept { get; init; }

    /// <summary>
    /// Request payload bytes. <see langword="null"/> or an empty array mean no payload;
    /// POST/PUT requests typically set this to a non-empty value.
    /// <para>
    /// The assigned value is copied; the getter returns the library's internal array, which must not
    /// be mutated by callers.
    /// </para>
    /// </summary>
    public byte[]? Payload
    {
        get;
        init => field = value is null ? null : (byte[])value.Clone();
    }
}
