namespace Tinkwell.Coap;

/// <summary>
/// Well-known CoAP option numbers.
/// </summary>
/// <remarks>
/// <para>
/// CoAP options are identified by an integer number and carry opaque bytes whose interpretation
/// depends on the option. Critical options (odd numbers) must be understood by the recipient;
/// elective options (even numbers) can be safely ignored. See RFC 7252, Section 5.4.
/// </para>
/// <para>
/// Only options actually used by this library are exposed here. The full IANA registry is at
/// <see href="https://www.iana.org/assignments/core-parameters/core-parameters.xhtml"/>.
/// </para>
/// </remarks>
public static class CoapOptionNumber
{
    /// <summary>RFC 7641, Section 2 - Observe registration/notification.</summary>
    public const int Observe = 6;

    /// <summary>RFC 7252, Section 5.10.7 - Location-Path segment in responses.</summary>
    public const int LocationPath = 8;

    /// <summary>RFC 7252, Section 5.10.1 - URI path segment.</summary>
    public const int UriPath = 11;

    /// <summary>RFC 7252, Section 5.10.3 - Content encoding format of the payload.</summary>
    public const int ContentFormat = 12;

    /// <summary>RFC 7252, Section 5.10.1 - URI query component.</summary>
    public const int UriQuery = 15;

    /// <summary>RFC 7252, Section 5.10.4 - Preferred response content-format.</summary>
    public const int Accept = 17;

    /// <summary>RFC 7959, Section 2.1 - Block2 option for blockwise GET responses.</summary>
    public const int Block2 = 23;

    /// <summary>RFC 7959, Section 2.2 - Block1 option for blockwise PUT/POST requests.</summary>
    public const int Block1 = 27;

    /// <summary>RFC 7959, Section 4 - Size2 option indicating total response size.</summary>
    public const int Size2 = 28;

    /// <summary>RFC 7959, Section 4 - Size1 option indicating total request payload size.</summary>
    public const int Size1 = 60;
}

/// <summary>
/// A parsed CoAP option: the option number and the raw bytes of its value.
/// </summary>
/// <remarks>
/// <para>
/// CoAP options (RFC 7252, Section 3.1) are encoded as opaque byte strings. The interpretation
/// depends on the option: some are strings (e.g. Uri-Path), some are unsigned integers
/// (e.g. Content-Format, Block1, Size1), some are opaque (e.g. ETag).
/// </para>
/// <para>
/// This type is the raw parsed form; higher-level accessors on <see cref="CoapMessage"/>
/// (<see cref="CoapMessage.UriPath"/>, <see cref="CoapMessage.Block2"/>, etc.) decode these
/// values into strongly-typed representations.
/// </para>
/// <example>
/// <para>Inspect options after parsing, using <see cref="CoapOptionNumber"/> for well-known numbers:</para>
/// <code>
/// var msg = CoapMessage.Parse(datagram);
/// foreach (var opt in msg.Options)
/// {
///     if (opt.Number == CoapOptionNumber.ContentFormat)
///     {
///         var format = (CoapContentFormat)opt.AsUInt();
///     }
/// }
/// </code>
/// </example>
/// </remarks>
/// <param name="Number">The CoAP option number. See <see cref="CoapOptionNumber"/>.</param>
/// <param name="Value">Raw option value bytes. Empty for zero-length options.</param>
public readonly record struct CoapOption(int Number, byte[] Value)
{
    /// <summary>
    /// Decodes the option value as an unsigned integer (RFC 7252, Section 3.2).
    /// </summary>
    /// <remarks>
    /// CoAP uint-encoded options are 0 to 4 bytes in network byte order (big-endian) and omit
    /// leading zero bytes. Zero-length values decode to 0.
    /// </remarks>
    /// <returns>The decoded non-negative integer value.</returns>
    /// <exception cref="InvalidOperationException">
    /// The option value is longer than 4 bytes, which is malformed per RFC 7252, Section 3.2.
    /// </exception>
    /// <exception cref="OverflowException">
    /// The decoded value exceeds <see cref="int.MaxValue"/> (i.e. the top bit of a 4-byte value is set).
    /// Callers that need to handle full <c>uint32</c> ranges should decode the <see cref="Value"/>
    /// bytes manually.
    /// </exception>
    /// <example>
    /// <para>Read the Content-Format option as an IANA-registered value.</para>
    /// <code>
    /// if (opt.Number == CoapOptionNumber.ContentFormat)
    /// {
    ///     var fmt = (CoapContentFormat)opt.AsUInt();
    ///     if (fmt == CoapContentFormat.ApplicationJson) { }
    /// }
    /// </code>
    /// </example>
    public int AsUInt()
    {
        uint value = Value.Length switch
        {
            0 => 0u,
            1 => Value[0],
            2 => ((uint)Value[0] << 8) | Value[1],
            3 => ((uint)Value[0] << 16) | ((uint)Value[1] << 8) | Value[2],
            4 => ((uint)Value[0] << 24) | ((uint)Value[1] << 16) | ((uint)Value[2] << 8) | Value[3],
            _ => throw new InvalidOperationException(
                $"Option {Number} value is {Value.Length} bytes, expected 0-4"),
        };

        if (value > int.MaxValue)
            throw new OverflowException(
                $"Option {Number} value {value} exceeds int.MaxValue");

        return (int)value;
    }

    /// <summary>Decodes the option value as a UTF-8 string.</summary>
    /// <returns>The option value interpreted as UTF-8 text.</returns>
    /// <example>
    /// <para>Extract each <c>Uri-Path</c> segment when building a path without using <see cref="CoapMessage.UriPath"/>.</para>
    /// <code>
    /// if (opt.Number == CoapOptionNumber.UriPath)
    /// {
    ///     var segment = opt.AsString();
    ///     Console.WriteLine(segment);
    /// }
    /// </code>
    /// </example>
    public string AsString() => System.Text.Encoding.UTF8.GetString(Value);
}
