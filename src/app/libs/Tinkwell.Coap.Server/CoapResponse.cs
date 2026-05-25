using System.Text;

namespace Tinkwell.Coap.Server;

/// <summary>
/// Represents a CoAP response produced by an <see cref="ICoapRequestHandler"/>
/// (RFC 7252, Section 5.5).
/// </summary>
/// <remarks>
/// <para>
/// Instances are immutable: construct them with the provided factory methods or with C# object
/// initialiser syntax and return them from a handler. Once returned, the server will not mutate
/// the <see cref="Payload"/> buffer. Callers should also avoid mutating any <c>byte[]</c> passed
/// to a factory after the response has been created, although the <c>Payload</c> setter does a
/// defensive copy to guard against surprise mutations.
/// </para>
/// <para>Example:</para>
/// <code>
/// server.MapGet("/status", (request, ct) =>
///     Task.FromResult(CoapResponse.Content(
///         System.Text.Encoding.UTF8.GetBytes("ok"),
///         CoapContentFormat.TextPlain)));
/// </code>
/// </remarks>
public sealed class CoapResponse
{
    /// <summary>
    /// CoAP response code as defined in RFC 7252, Section 12.1.2 (e.g. <see cref="CoapCode.Content"/>,
    /// <see cref="CoapCode.NotFound"/>).
    /// </summary>
    public byte Code { get; init; }

    /// <summary>
    /// Response payload bytes, or <see langword="null"/> for an empty payload.
    /// </summary>
    /// <remarks>
    /// The setter creates a defensive copy of the supplied array so that subsequent mutations by
    /// the caller do not affect the response.
    /// </remarks>
    public byte[]? Payload
    {
        get;
        init => field = value is null ? null : (byte[])value.Clone();
    }

    /// <summary>
    /// Content-Format of the response payload (RFC 7252, Section 5.10.3), or <see langword="null"/>
    /// when no payload (or an opaque one) is being returned.
    /// </summary>
    public CoapContentFormat? ContentFormat { get; init; }

    /// <summary>Block1 option to echo in the response (RFC 7959, Section 2.5).</summary>
    /// <remarks>
    /// Set this when responding to a Block1 upload chunk, typically with <c>2.31 Continue</c> for
    /// intermediate blocks or the final success code for the last block. See
    /// <see cref="Continue(CoapBlockOption)"/> for the common case.
    /// </remarks>
    public CoapBlockOption? Block1 { get; init; }

    /// <summary>Block2 option describing block metadata in the response (RFC 7959, Section 2.5).</summary>
    /// <remarks>
    /// Setting this instructs <see cref="CoapServer"/> to skip transparent Block2 splitting for
    /// this response: the handler is taking full control of the blockwise exchange and the
    /// <see cref="Payload"/> is expected to be exactly one block. Leave <see langword="null"/>
    /// (the default) to let the server cache the full payload, split it according to
    /// <see cref="CoapServerOptions.ResponseBlockSize"/>, and serve follow-up blocks from its
    /// cache.
    /// </remarks>
    public CoapBlockOption? Block2 { get; init; }

    /// <summary>Creates a <c>2.05 Content</c> response (RFC 7252, Section 5.9.1.5).</summary>
    /// <param name="payload">The response payload. Cannot be <see langword="null"/>.</param>
    /// <param name="contentFormat">The content format of <paramref name="payload"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="payload"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <para>Return sensor readings with an explicit <see cref="CoapContentFormat"/>.</para>
    /// <code>
    /// return CoapResponse.Content(
    ///     Encoding.UTF8.GetBytes("22.1"),
    ///     CoapContentFormat.TextPlain);
    /// </code>
    /// </example>
    public static CoapResponse Content(byte[] payload, CoapContentFormat contentFormat)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new CoapResponse
        {
            Code = CoapCode.Content,
            Payload = payload,
            ContentFormat = contentFormat,
        };
    }

    /// <summary>
    /// Creates a <c>2.01 Created</c> response (RFC 7252, Section 5.9.1.1), optionally with a payload.
    /// </summary>
    /// <param name="payload">Optional payload describing the newly-created resource.</param>
    /// <param name="contentFormat">Content format of <paramref name="payload"/>; ignored when <paramref name="payload"/> is <see langword="null"/>.</param>
    /// <example>
    /// <para>Typical POST handler response after creating a new resource on the server.</para>
    /// <code>
    /// return CoapResponse.Created(
    ///     System.Text.Encoding.UTF8.GetBytes($"/{newId}"),
    ///     CoapContentFormat.TextPlain);
    /// </code>
    /// </example>
    public static CoapResponse Created(byte[]? payload = null, CoapContentFormat? contentFormat = null) => new()
    {
        Code = CoapCode.Created,
        Payload = payload,
        ContentFormat = contentFormat,
    };

    /// <summary>Creates a <c>2.04 Changed</c> response (RFC 7252, Section 5.9.1.4).</summary>
    public static CoapResponse Changed() => new() { Code = CoapCode.Changed };

    /// <summary>Creates a <c>2.02 Deleted</c> response (RFC 7252, Section 5.9.1.2).</summary>
    public static CoapResponse Deleted() => new() { Code = CoapCode.Deleted };

    /// <summary>
    /// Creates a <c>4.03 Forbidden</c> response (RFC 7252, Section 5.9.2.3), optionally with a
    /// text/plain diagnostic message.
    /// </summary>
    /// <param name="message">Optional UTF-8 message to send as the payload.</param>
    public static CoapResponse Forbidden(string? message = null) => BuildText(CoapCode.Forbidden, message);

    /// <summary>Creates a <c>4.04 Not Found</c> response (RFC 7252, Section 5.9.2.4).</summary>
    /// <example>
    /// <para>Use when the path did not map to a resource in your store.</para>
    /// <code>
    /// if (resource is null)
    ///     return CoapResponse.NotFound();
    /// </code>
    /// </example>
    public static CoapResponse NotFound() => new() { Code = CoapCode.NotFound };

    /// <summary>
    /// Creates a <c>4.00 Bad Request</c> response (RFC 7252, Section 5.9.2.1), optionally with a
    /// text/plain diagnostic message.
    /// </summary>
    /// <param name="message">Optional UTF-8 message to send as the payload.</param>
    public static CoapResponse BadRequest(string? message = null) => BuildText(CoapCode.BadRequest, message);

    /// <summary>Creates a <c>4.05 Method Not Allowed</c> response (RFC 7252, Section 5.9.2.5).</summary>
    public static CoapResponse MethodNotAllowed() => new() { Code = CoapCode.MethodNotAllowed };

    /// <summary>
    /// Creates a <c>2.31 Continue</c> response (RFC 7959, Section 2.9.1) echoing the client's
    /// Block1 option to acknowledge receipt of one block in a blockwise upload.
    /// </summary>
    /// <param name="block1Echo">The Block1 option value from the client's request.</param>
    public static CoapResponse Continue(CoapBlockOption block1Echo) => new()
    {
        Code = CoapCode.Continue,
        Block1 = block1Echo,
    };

    /// <summary>
    /// Creates a <c>4.08 Request Entity Incomplete</c> response (RFC 7959, Section 2.9.2),
    /// optionally with a text/plain diagnostic message.
    /// </summary>
    /// <param name="message">Optional UTF-8 message to send as the payload.</param>
    public static CoapResponse RequestEntityIncomplete(string? message = null)
        => BuildText(CoapCode.RequestEntityIncomplete, message);

    /// <summary>
    /// Creates a <c>4.13 Request Entity Too Large</c> response (RFC 7959, Section 2.9.3).
    /// </summary>
    public static CoapResponse RequestEntityTooLarge() => new()
    {
        Code = CoapCode.RequestEntityTooLarge,
    };

    /// <summary>
    /// Creates a <c>5.00 Internal Server Error</c> response (RFC 7252, Section 5.9.3.1),
    /// optionally with a text/plain diagnostic message.
    /// </summary>
    /// <param name="message">Optional UTF-8 message to send as the payload.</param>
    /// <example>
    /// <para>Fallback when a handler or dependency throws; prefer mapping known failures with an exception filter.</para>
    /// <code>
    /// return CoapResponse.InternalError("unexpected failure in sensor pipeline");
    /// </code>
    /// </example>
    public static CoapResponse InternalError(string? message = null)
        => BuildText(CoapCode.InternalServerError, message);

    private static CoapResponse BuildText(byte code, string? message) => new()
    {
        Code = code,
        Payload = message is not null ? Encoding.UTF8.GetBytes(message) : null,
        ContentFormat = message is not null ? CoapContentFormat.TextPlain : null,
    };
}
