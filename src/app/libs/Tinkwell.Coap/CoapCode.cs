namespace Tinkwell.Coap;

/// <summary>
/// CoAP request method codes (RFC 7252, Section 5.8).
/// </summary>
/// <remarks>
/// The underlying byte values match the CoAP wire encoding: a request code is
/// <c>(0 &lt;&lt; 5) | detail</c>, where class 0 identifies a request and <c>detail</c> is the
/// method (1 = GET, 2 = POST, 3 = PUT, 4 = DELETE). Cast to <see cref="byte"/> to get the raw
/// wire code; compare against <see cref="CoapCode"/> constants to inspect parsed messages.
/// </remarks>
public enum CoapMethod : byte
{
    /// <summary>0.01 GET - Retrieves the representation of a resource.</summary>
    Get = CoapCode.Get,

    /// <summary>0.02 POST - Processes the representation enclosed in the request.</summary>
    Post = CoapCode.Post,

    /// <summary>0.03 PUT - Updates or creates the target resource.</summary>
    Put = CoapCode.Put,

    /// <summary>0.04 DELETE - Deletes the target resource.</summary>
    Delete = CoapCode.Delete,
}

/// <summary>
/// CoAP request and response codes (RFC 7252, Section 12.1).
/// </summary>
/// <remarks>
/// <para>
/// Codes are encoded as a single byte: <c>(class &lt;&lt; 5) | detail</c>, where the top 3 bits
/// identify the code class (0 = request, 2 = success, 4 = client error, 5 = server error) and the
/// bottom 5 bits identify the specific code within that class. The dotted form <c>c.dd</c>
/// commonly seen in CoAP literature is produced by <see cref="ToDisplayString(byte)"/>.
/// </para>
/// <para>
/// Only the codes actually used in this library and by its callers are exposed as constants.
/// The full registry is maintained by IANA and can be consulted for any additional code values.
/// </para>
/// </remarks>
public static class CoapCode
{
    private const int ClassShift = 5;
    private const int DetailMask = 0x1F;

    /// <summary>0.01 GET - Retrieves the representation of a resource.</summary>
    public const byte Get = 0x01;

    /// <summary>0.02 POST - Processes the representation enclosed in the request.</summary>
    public const byte Post = 0x02;

    /// <summary>0.03 PUT - Updates or creates the target resource.</summary>
    public const byte Put = 0x03;

    /// <summary>0.04 DELETE - Deletes the target resource.</summary>
    public const byte Delete = 0x04;

    /// <summary>2.01 Created - The resource has been created (RFC 7252, Section 5.9.1.1).</summary>
    public const byte Created = 0x41;

    /// <summary>2.02 Deleted - The resource has been deleted (RFC 7252, Section 5.9.1.2).</summary>
    public const byte Deleted = 0x42;

    /// <summary>
    /// 2.03 Valid - The resource has not changed since the version identified by the request's
    /// ETag (RFC 7252, Section 5.9.1.3); commonly used as a registration-success code for
    /// conditional Observe requests (RFC 7641, Section 4.2).
    /// </summary>
    public const byte Valid = 0x43;

    /// <summary>2.04 Changed - The resource has been updated (RFC 7252, Section 5.9.1.4).</summary>
    public const byte Changed = 0x44;

    /// <summary>2.05 Content - The payload contains the current representation (RFC 7252, Section 5.9.1.5).</summary>
    public const byte Content = 0x45;

    /// <summary>
    /// 2.31 Continue - Acknowledges a non-final block in a Block1 upload (RFC 7959, Section 2.9.1).
    /// Returned by the server to confirm that a Block1 chunk was received and that the client may send the next one.
    /// </summary>
    public const byte Continue = 0x5F;

    /// <summary>4.00 Bad Request - The request could not be understood (RFC 7252, Section 5.9.2.1).</summary>
    public const byte BadRequest = 0x80;

    /// <summary>4.03 Forbidden - The server refuses to authorize the request (RFC 7252, Section 5.9.2.3).</summary>
    public const byte Forbidden = 0x83;

    /// <summary>4.04 Not Found - No resource matches the request URI (RFC 7252, Section 5.9.2.4).</summary>
    public const byte NotFound = 0x84;

    /// <summary>4.05 Method Not Allowed - The method is not allowed for the target resource (RFC 7252, Section 5.9.2.5).</summary>
    public const byte MethodNotAllowed = 0x85;

    /// <summary>
    /// 4.08 Request Entity Incomplete - Block sequence error in a blockwise upload (RFC 7959, Section 2.9.2).
    /// Returned when the server cannot reassemble the payload (missing blocks, out-of-order, or size mismatch).
    /// </summary>
    public const byte RequestEntityIncomplete = 0x88;

    /// <summary>
    /// 4.12 Precondition Failed - A request precondition (e.g. <c>If-Match</c>) was not met
    /// (RFC 7252, Section 5.9.2.8).
    /// </summary>
    public const byte PreconditionFailed = 0x8C;

    /// <summary>
    /// 4.13 Request Entity Too Large - The payload exceeds the server's capacity (RFC 7252, Section 5.9.2.9;
    /// RFC 7959, Section 2.9.3). May include a Size1 option advertising the maximum size the server will accept.
    /// </summary>
    public const byte RequestEntityTooLarge = 0x8D;

    /// <summary>
    /// 4.15 Unsupported Content-Format - The request payload is in a format the server does not
    /// understand (RFC 7252, Section 5.9.2.10).
    /// </summary>
    public const byte UnsupportedContentFormat = 0x8F;

    /// <summary>5.00 Internal Server Error - Unexpected server condition (RFC 7252, Section 5.9.3.1).</summary>
    public const byte InternalServerError = 0xA0;

    /// <summary>5.03 Service Unavailable - The server is temporarily unable to handle the request (RFC 7252, Section 5.9.3.4).</summary>
    public const byte ServiceUnavailable = 0xA3;

    /// <summary>
    /// Returns the human-readable method name for a request code.
    /// </summary>
    /// <param name="code">A request code (class 0).</param>
    /// <returns>
    /// One of <c>"GET"</c>, <c>"POST"</c>, <c>"PUT"</c>, <c>"DELETE"</c> for the standard methods;
    /// otherwise the dotted form (e.g. <c>"0.05"</c>) for unknown request codes.
    /// </returns>
    /// <example>
    /// <para>Turn a request code from <see cref="CoapMessage.Code"/> into a log-friendly method name.</para>
    /// <code>
    /// if (message.Code &lt; 0x20)
    ///     Console.WriteLine(CoapCode.ToMethodString(message.Code)); // e.g. "GET" for 0.01
    /// </code>
    /// </example>
    public static string ToMethodString(byte code) => code switch
    {
        Get => "GET",
        Post => "POST",
        Put => "PUT",
        Delete => "DELETE",
        _ => $"0.{code:D2}",
    };

    /// <summary>
    /// Formats a code as its canonical dotted display form <c>c.dd</c>.
    /// </summary>
    /// <param name="code">Any CoAP code byte.</param>
    /// <returns>
    /// The dotted representation, e.g. <c>"2.05"</c> for <see cref="Content"/>, <c>"4.04"</c> for
    /// <see cref="NotFound"/>.
    /// </returns>
    /// <example>
    /// <para>Format any class/detail byte (requests and responses) for UI or logging.</para>
    /// <code>
    /// var label = CoapCode.ToDisplayString(CoapCode.Changed); // "2.04"
    /// var err = CoapCode.ToDisplayString(CoapCode.ServiceUnavailable); // "5.03"
    /// </code>
    /// </example>
    public static string ToDisplayString(byte code)
    {
        int cls = code >> ClassShift;
        int detail = code & DetailMask;
        return $"{cls}.{detail:D2}";
    }
}
