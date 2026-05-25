using Tinkwell.Coap;

namespace Tinkwell.Encoding;

/// <summary>
/// Dispatches payload decoding to the appropriate codec based on the CoAP Content-Format option
/// (RFC 7252, Section 12.3).
/// </summary>
/// <remarks>
/// <para>This is a convenience facade for the common case of decoding a single resource from a
/// CoAP response payload. For full-fidelity decoding (multiple records, base names, timestamps),
/// call <see cref="TlvDecoder"/> or <see cref="SenmlJsonCodec"/> directly.</para>
/// </remarks>
public static class PayloadCodec
{
    /// <summary>
    /// Returns <see langword="true"/> if <see cref="DecodeSingleResource(System.ReadOnlySpan{byte}, CoapContentFormat, PayloadType)"/>
    /// can handle the given content-format. Currently: <see cref="CoapContentFormat.TextPlain"/>,
    /// <see cref="CoapContentFormat.ApplicationOctetStream"/>,
    /// <see cref="CoapContentFormat.ApplicationLwm2mTlv"/>, and
    /// <see cref="CoapContentFormat.ApplicationSenmlJson"/>.
    /// </summary>
    public static bool IsSupported(CoapContentFormat contentFormat) => contentFormat is
        CoapContentFormat.TextPlain or
        CoapContentFormat.ApplicationOctetStream or
        CoapContentFormat.ApplicationLwm2mTlv or
        CoapContentFormat.ApplicationSenmlJson;

    /// <summary>
    /// Decodes a single resource value from a payload based on its content-format.
    /// </summary>
    /// <param name="payload">The raw payload bytes.</param>
    /// <param name="contentFormat">The CoAP content-format identifier.</param>
    /// <param name="expectedType">
    /// The expected semantic type of the value. Used by <see cref="CoapContentFormat.TextPlain"/>
    /// (to choose the parsing rule) and by <see cref="CoapContentFormat.ApplicationLwm2mTlv"/>
    /// (to interpret the first record's raw bytes). It is <i>not</i> used by
    /// <see cref="CoapContentFormat.ApplicationSenmlJson"/> (where the type is fixed by the value
    /// field present in the JSON) nor by <see cref="CoapContentFormat.ApplicationOctetStream"/>
    /// (which always returns <see cref="PayloadType.Opaque"/>).
    /// </param>
    /// <returns>
    /// For TLV the first record's value (<see cref="PayloadValue.Empty"/> if the payload is empty);
    /// for SenML the first record's value (<see cref="PayloadValue.Empty"/> if the array is empty);
    /// for text/plain a value parsed per <paramref name="expectedType"/> (falling back to a string
    /// when the text does not parse); for octet-stream an opaque copy of the bytes.
    /// </returns>
    /// <exception cref="NotSupportedException"><paramref name="contentFormat"/> is not in <see cref="IsSupported"/>.</exception>
    /// <exception cref="FormatException">The payload is malformed for its declared content-format.</exception>
    public static PayloadValue DecodeSingleResource(
        ReadOnlySpan<byte> payload,
        CoapContentFormat contentFormat,
        PayloadType expectedType = PayloadType.Float)
        => DecodeSingleResource(payload, contentFormat, expectedType, now: null);

    /// <summary>
    /// Decodes a single resource value with a caller-supplied reference "now" used by SenML
    /// relative-time resolution (RFC 8428, Section 4.5.3). Behaves identically to
    /// <see cref="DecodeSingleResource(System.ReadOnlySpan{byte}, CoapContentFormat, PayloadType)"/>
    /// for all other content-formats.
    /// </summary>
    /// <param name="payload">The raw payload bytes.</param>
    /// <param name="contentFormat">The CoAP content-format identifier.</param>
    /// <param name="expectedType">The expected semantic type (see the other overload's documentation for which formats consume it).</param>
    /// <param name="now">Reference time for SenML relative timestamps; <see langword="null"/> uses <see cref="DateTimeOffset.UtcNow"/>.</param>
    /// <exception cref="NotSupportedException"><paramref name="contentFormat"/> is not supported.</exception>
    /// <exception cref="FormatException">The payload is malformed.</exception>
    public static PayloadValue DecodeSingleResource(
        ReadOnlySpan<byte> payload,
        CoapContentFormat contentFormat,
        PayloadType expectedType,
        DateTimeOffset? now)
    {
        return contentFormat switch
        {
            CoapContentFormat.TextPlain => DecodeTextPlain(payload, expectedType),
            CoapContentFormat.ApplicationLwm2mTlv => DecodeTlvSingle(payload, expectedType),
            CoapContentFormat.ApplicationSenmlJson => DecodeSenmlJsonSingle(payload, now),
            CoapContentFormat.ApplicationOctetStream =>
                PayloadValue.FromOpaque(payload.ToArray()),
            _ => throw new NotSupportedException($"Content-format {contentFormat} is not supported"),
        };
    }

    private static PayloadValue DecodeTextPlain(
        ReadOnlySpan<byte> payload, PayloadType expectedType)
    {
        var text = System.Text.Encoding.UTF8.GetString(payload).Trim();
        return expectedType switch
        {
            PayloadType.Float when double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d) =>
                PayloadValue.FromFloat(d),
            PayloadType.Integer when long.TryParse(text, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var l) =>
                PayloadValue.FromInteger(l),
            PayloadType.Boolean when bool.TryParse(text, out var b) =>
                PayloadValue.FromBoolean(b),
            _ => PayloadValue.FromString(text),
        };
    }

    private static PayloadValue DecodeTlvSingle(
        ReadOnlySpan<byte> payload, PayloadType expectedType)
    {
        var records = TlvDecoder.Decode(payload);
        if (records.Count == 0)
            return PayloadValue.Empty;
        return TlvDecoder.Interpret(records[0].RawValue, expectedType);
    }

    private static PayloadValue DecodeSenmlJsonSingle(ReadOnlySpan<byte> payload, DateTimeOffset? now)
    {
        var records = SenmlJsonCodec.Decode(payload, now);
        return records.Count > 0 ? records[0].Value : PayloadValue.Empty;
    }
}
