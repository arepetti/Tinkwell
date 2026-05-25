using System.Buffers.Binary;

namespace Tinkwell.Encoding;

/// <summary>
/// Decodes LwM2M TLV (Type-Length-Value) payloads into structured records.
/// </summary>
/// <remarks>
/// <para>
/// Format defined in OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3. <see cref="Decode"/> performs
/// a flat scan: nested values inside <see cref="TlvRecordType.ObjectInstance"/> or
/// <see cref="TlvRecordType.MultipleResource"/> records are returned as opaque <c>RawValue</c>
/// bytes; call <see cref="Decode"/> again on those bytes to recurse.
/// </para>
/// <para>
/// All methods are pure and thread-safe. Malformed input produces a <see cref="FormatException"/>;
/// unsupported payload types in <see cref="Interpret"/> produce an <see cref="ArgumentException"/>.
/// </para>
/// </remarks>
public static class TlvDecoder
{
    /// <summary>
    /// Decodes all TLV records from a byte buffer.
    /// </summary>
    /// <param name="data">The raw TLV bytes.</param>
    /// <returns>The decoded records, in source order. Empty input yields an empty list.</returns>
    /// <exception cref="FormatException">The buffer is truncated or otherwise malformed.</exception>
    public static IReadOnlyList<DecodedTlvRecord> Decode(ReadOnlySpan<byte> data)
    {
        var records = new List<DecodedTlvRecord>();
        int offset = 0;

        while (offset < data.Length)
        {
            var record = DecodeRecord(data, ref offset);
            records.Add(record);
        }

        return records;
    }

    // OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3, Figure 14
    private const int TypeIdentifierShift = 6;
    private const int TypeIdentifierMask = 0x03;
    private const byte IdentifierLengthBit = 0x20;    // bit 5
    private const int LengthOfLengthShift = 3;
    private const int LengthOfLengthMask = 0x03;
    private const int InlineLengthMask = 0x07;

    private static DecodedTlvRecord DecodeRecord(ReadOnlySpan<byte> data, ref int offset)
    {
        if (offset >= data.Length)
            throw new FormatException("Unexpected end of TLV data");

        byte typeByte = data[offset++];

        var recordType = (TlvRecordType)((typeByte >> TypeIdentifierShift) & TypeIdentifierMask);
        bool idIs16Bit = (typeByte & IdentifierLengthBit) != 0;
        int lengthOfLength = (typeByte >> LengthOfLengthShift) & LengthOfLengthMask;
        int inlineLength = typeByte & InlineLengthMask;

        int identifier;
        if (idIs16Bit)
        {
            if (offset + 2 > data.Length)
                throw new FormatException("Unexpected end of TLV data reading 16-bit identifier");
            identifier = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
            offset += 2;
        }
        else
        {
            if (offset >= data.Length)
                throw new FormatException("Unexpected end of TLV data reading 8-bit identifier");
            identifier = data[offset++];
        }

        int valueLength;
        if (lengthOfLength == 0)
        {
            valueLength = inlineLength;
        }
        else
        {
            // Wire format caps `lengthOfLength` at 3 bytes (Figure 14), so `valueLength` fits in
            // 24 bits and cannot become negative through the loop. The defensive check below still
            // guards against pathological state.
            valueLength = 0;
            if (offset + lengthOfLength > data.Length)
                throw new FormatException("Unexpected end of TLV data reading length field");
            for (int i=0; i < lengthOfLength; ++i)
                valueLength = (valueLength << 8) | data[offset++];
        }

        // Use a long sum to avoid signed-int overflow when `offset` and `valueLength` are both
        // large; `valueLength < 0` cannot happen with valid wire input but is checked defensively.
        if (valueLength < 0 || (long)offset + valueLength > data.Length)
            throw new FormatException(
                $"TLV value length {valueLength} exceeds remaining data ({data.Length - offset} bytes)");

        var rawValue = data.Slice(offset, valueLength).ToArray();
        offset += valueLength;

        return new DecodedTlvRecord(recordType, identifier, rawValue);
    }

    /// <summary>
    /// Interprets the raw bytes of a decoded TLV record as a typed <see cref="PayloadValue"/>.
    /// </summary>
    /// <param name="rawValue">The raw value bytes, as returned by <see cref="DecodedTlvRecord.RawValue"/>.</param>
    /// <param name="type">The expected semantic type of the value.</param>
    /// <returns>A typed payload value.</returns>
    /// <exception cref="FormatException">The byte length is not valid for <paramref name="type"/> (for integer/float/object-link).</exception>
    /// <exception cref="ArgumentException"><paramref name="type"/> is not a supported value.</exception>
    public static PayloadValue Interpret(byte[] rawValue, PayloadType type)
    {
        ArgumentNullException.ThrowIfNull(rawValue);

        return type switch
        {
            PayloadType.String => PayloadValue.FromString(System.Text.Encoding.UTF8.GetString(rawValue)),
            PayloadType.Integer => PayloadValue.FromInteger(DecodeSignedInteger(rawValue)),
            PayloadType.Float => PayloadValue.FromFloat(DecodeFloat(rawValue)),
            PayloadType.Boolean => PayloadValue.FromBoolean(rawValue.Length > 0 && rawValue[0] != 0),
            PayloadType.Opaque => PayloadValue.FromOpaque(rawValue),
            PayloadType.Time => PayloadValue.FromTime(DateTimeOffset.FromUnixTimeSeconds(DecodeSignedInteger(rawValue))),
            PayloadType.ObjectLink => PayloadValue.FromObjectLink(DecodeObjectLink(rawValue)),
            PayloadType.None => PayloadValue.Empty,
            _ => throw new ArgumentException($"Unsupported payload type: {type}", nameof(type)),
        };
    }

    /// <summary>
    /// Decodes a big-endian signed integer from 0, 1, 2, 4 or 8 bytes
    /// (OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3).
    /// </summary>
    /// <exception cref="FormatException">The buffer length is not 0, 1, 2, 4 or 8 bytes.</exception>
    internal static long DecodeSignedInteger(byte[] data) => data.Length switch
    {
        0 => 0,
        1 => (sbyte)data[0],
        2 => BinaryPrimitives.ReadInt16BigEndian(data),
        4 => BinaryPrimitives.ReadInt32BigEndian(data),
        8 => BinaryPrimitives.ReadInt64BigEndian(data),
        _ => throw new FormatException($"Invalid integer TLV length: {data.Length} (expected 0, 1, 2, 4, or 8)"),
    };

    /// <summary>
    /// Decodes a big-endian IEEE 754 float from 4 or 8 bytes
    /// (OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3).
    /// </summary>
    /// <exception cref="FormatException">The buffer length is not 4 or 8 bytes.</exception>
    internal static double DecodeFloat(byte[] data) => data.Length switch
    {
        4 => BinaryPrimitives.ReadSingleBigEndian(data),
        8 => BinaryPrimitives.ReadDoubleBigEndian(data),
        _ => throw new FormatException($"Invalid float TLV length: {data.Length} (expected 4 or 8)"),
    };

    /// <summary>
    /// Decodes an LwM2M object link from exactly four big-endian bytes
    /// (OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3).
    /// </summary>
    /// <exception cref="FormatException">The buffer length is not 4 bytes.</exception>
    internal static ObjectLink DecodeObjectLink(byte[] data)
    {
        if (data.Length != 4)
            throw new FormatException($"Invalid object-link TLV length: {data.Length} (expected 4)");

        int objectId = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(0, 2));
        int instanceId = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2, 2));
        return new ObjectLink(objectId, instanceId);
    }
}

/// <summary>
/// A decoded TLV record with its raw value bytes.
/// </summary>
/// <param name="Type">The TLV record kind (resource, object instance, etc.).</param>
/// <param name="Identifier">The 16-bit identifier (resource ID, instance ID, etc.).</param>
/// <param name="RawValue">The raw value bytes; pass to <see cref="TlvDecoder.Interpret"/> to obtain a typed value.</param>
/// <remarks>
/// For wrapper records (<see cref="TlvRecordType.ObjectInstance"/>, <see cref="TlvRecordType.MultipleResource"/>)
/// the raw value is itself a sequence of TLV records — call <see cref="TlvDecoder.Decode"/> on it to recurse.
/// </remarks>
public sealed record DecodedTlvRecord(
    TlvRecordType Type,
    int Identifier,
    byte[] RawValue);
