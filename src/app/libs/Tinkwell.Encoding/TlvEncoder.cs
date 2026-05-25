using System.Buffers.Binary;

namespace Tinkwell.Encoding;

/// <summary>
/// Encodes LwM2M TLV (Type-Length-Value) records.
/// </summary>
/// <remarks>
/// <para>
/// Format defined in OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3. The encoder produces a flat
/// sequence of records: it does not synthesize wrapping <see cref="TlvRecordType.ObjectInstance"/> or
/// <see cref="TlvRecordType.MultipleResource"/> envelopes for you. To encode a nested structure,
/// encode the inner records separately, wrap the resulting bytes in a single
/// <see cref="TlvRecord"/> of the desired wrapper type using <see cref="PayloadType.Opaque"/>, and
/// encode that.
/// </para>
/// <para>All methods on this type are pure and thread-safe.</para>
/// </remarks>
public static class TlvEncoder
{
    /// <summary>
    /// Encodes a sequence of TLV records into a contiguous byte array.
    /// </summary>
    /// <param name="records">The records to encode (in order).</param>
    /// <returns>A newly allocated array containing the concatenated TLV records.</returns>
    /// <exception cref="ArgumentException">A record carries an unsupported <see cref="PayloadType"/> for its declared <see cref="TlvRecord.ValueType"/>.</exception>
    public static byte[] Encode(IReadOnlyList<TlvRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        using var ms = new MemoryStream();
        for (int i=0; i < records.Count; ++i)
            WriteRecord(ms, records[i]);
        return ms.ToArray();
    }

    /// <summary>
    /// Encodes a single TLV record into a byte array.
    /// </summary>
    /// <param name="record">The record to encode.</param>
    /// <returns>A newly allocated array containing the encoded record.</returns>
    /// <exception cref="ArgumentException">The record carries an unsupported <see cref="PayloadType"/>.</exception>
    public static byte[] EncodeSingle(TlvRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var ms = new MemoryStream();
        WriteRecord(ms, record);
        return ms.ToArray();
    }

    private static void WriteRecord(MemoryStream ms, TlvRecord record)
    {
        if (record.Identifier is < 0 or > ushort.MaxValue)
            throw new ArgumentException(
                $"TLV identifier {record.Identifier} is out of range (0..65535).",
                nameof(record));

        var value = EncodeValue(record.Value, record.ValueType);

        // OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3, Figure 14:
        //   Bits 7-6: Type of Identifier
        //   Bit 5:    Length of Identifier (0 = 8-bit, 1 = 16-bit)
        //   Bits 4-3: Length of the Length field (00 = in bits 2-0, etc.)
        //   Bits 2-0: 3-bit length or part of type field
        byte typeByte = (byte)((int)record.Type << TypeIdentifierShift);

        bool idIs16Bit = record.Identifier > byte.MaxValue;
        if (idIs16Bit)
            typeByte |= IdentifierLengthBit;

        int valueLength = value.Length;

        if (valueLength < InlineLengthMax)
        {
            typeByte |= (byte)(valueLength & InlineLengthMask);
        }
        else if (valueLength <= byte.MaxValue)
        {
            typeByte |= LengthOfLength1;
        }
        else if (valueLength <= ushort.MaxValue)
        {
            typeByte |= LengthOfLength2;
        }
        else if (valueLength <= MaxValueLength)
        {
            typeByte |= LengthOfLength3;
        }
        else
        {
            throw new ArgumentException(
                $"TLV value length {valueLength} exceeds the maximum encodable size (24-bit, {MaxValueLength}).",
                nameof(record));
        }

        ms.WriteByte(typeByte);

        if (idIs16Bit)
        {
            Span<byte> idBytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(idBytes, (ushort)record.Identifier);
            ms.Write(idBytes);
        }
        else
        {
            ms.WriteByte((byte)record.Identifier);
        }

        if (valueLength >= InlineLengthMax)
        {
            if (valueLength <= byte.MaxValue)
            {
                ms.WriteByte((byte)valueLength);
            }
            else if (valueLength <= ushort.MaxValue)
            {
                Span<byte> lenBytes = stackalloc byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(lenBytes, (ushort)valueLength);
                ms.Write(lenBytes);
            }
            else
            {
                Span<byte> lenBytes = stackalloc byte[3];
                lenBytes[0] = (byte)((valueLength >> 16) & 0xFF);
                lenBytes[1] = (byte)((valueLength >> 8) & 0xFF);
                lenBytes[2] = (byte)(valueLength & 0xFF);
                ms.Write(lenBytes);
            }
        }

        ms.Write(value);
    }

    // OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3, Figure 14
    private const int TypeIdentifierShift = 6;
    private const byte IdentifierLengthBit = 0x20;  // bit 5
    private const int InlineLengthMax = 8;           // values 0-7 fit in bits 2-0
    private const byte InlineLengthMask = 0x07;
    private const byte LengthOfLength1 = 0x08;      // bits 4-3 = 01 → 1 byte follows
    private const byte LengthOfLength2 = 0x10;      // bits 4-3 = 10 → 2 bytes follow
    private const byte LengthOfLength3 = 0x18;      // bits 4-3 = 11 → 3 bytes follow
    private const int MaxValueLength = 0xFFFFFF;     // 24-bit length field

    /// <summary>
    /// Encodes a typed value into its TLV wire representation
    /// (OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3).
    /// </summary>
    /// <exception cref="ArgumentException">The <see cref="PayloadType"/> is not supported.</exception>
    internal static byte[] EncodeValue(PayloadValue value, PayloadType type) => type switch
    {
        PayloadType.String => System.Text.Encoding.UTF8.GetBytes(value.AsString()),
        PayloadType.Integer => EncodeSignedInteger(value.AsLong()),
        PayloadType.Float => EncodeFloat(value.AsDouble()),
        PayloadType.Boolean => [value.AsBoolean() ? (byte)1 : (byte)0],
        PayloadType.Opaque => (byte[])(value.RawValue ?? Array.Empty<byte>()),
        PayloadType.Time => EncodeSignedInteger(((DateTimeOffset)value.RawValue!).ToUnixTimeSeconds()),
        PayloadType.ObjectLink => EncodeObjectLink(value.AsObjectLink()),
        PayloadType.None => [],
        _ => throw new ArgumentException($"Unsupported payload type: {type}", nameof(type)),
    };

    /// <summary>
    /// Encodes a signed integer in the smallest big-endian representation that fits
    /// (1, 2, 4 or 8 bytes), per OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3.
    /// </summary>
    internal static byte[] EncodeSignedInteger(long value)
    {
        if (value is >= sbyte.MinValue and <= sbyte.MaxValue)
            return [(byte)(sbyte)value];

        if (value is >= short.MinValue and <= short.MaxValue)
        {
            var buf = new byte[2];
            BinaryPrimitives.WriteInt16BigEndian(buf, (short)value);
            return buf;
        }

        if (value is >= int.MinValue and <= int.MaxValue)
        {
            var buf = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(buf, (int)value);
            return buf;
        }

        {
            var buf = new byte[8];
            BinaryPrimitives.WriteInt64BigEndian(buf, value);
            return buf;
        }
    }

    /// <summary>
    /// Encodes a double as a 4-byte IEEE 754 float when the value round-trips through
    /// <see cref="float"/> losslessly, otherwise as an 8-byte double, in big-endian
    /// (OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3).
    /// </summary>
    internal static byte[] EncodeFloat(double value)
    {
        float f = (float)value;
        // Bit-comparison so that NaN/Infinity round-trip and -0.0 is preserved.
        if (BitConverter.DoubleToInt64Bits((double)f) == BitConverter.DoubleToInt64Bits(value))
        {
            var buf = new byte[4];
            BinaryPrimitives.WriteSingleBigEndian(buf, f);
            return buf;
        }

        {
            var buf = new byte[8];
            BinaryPrimitives.WriteDoubleBigEndian(buf, value);
            return buf;
        }
    }

    /// <summary>
    /// Encodes an LwM2M object link as four big-endian bytes: 16-bit object ID followed
    /// by 16-bit instance ID (OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3).
    /// </summary>
    internal static byte[] EncodeObjectLink(ObjectLink link)
    {
        var buf = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0, 2), (ushort)link.ObjectId);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(2, 2), (ushort)link.InstanceId);
        return buf;
    }
}

/// <summary>
/// TLV record type identifiers (OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3, Table 21).
/// </summary>
public enum TlvRecordType : byte
{
    /// <summary>An object instance, whose value is a flat sequence of inner TLV records.</summary>
    ObjectInstance = 0b00,

    /// <summary>A single resource instance within a multiple-resource record.</summary>
    ResourceInstance = 0b01,

    /// <summary>A "multiple resource" record whose value is a flat sequence of <see cref="ResourceInstance"/> records.</summary>
    MultipleResource = 0b10,

    /// <summary>A single resource record (the most common case).</summary>
    Resource = 0b11,
}

/// <summary>
/// A single TLV record to encode.
/// </summary>
/// <param name="Type">The TLV record kind (object instance, resource, etc.).</param>
/// <param name="Identifier">The 16-bit identifier (resource ID, instance ID, etc.). Range 0..65535.</param>
/// <param name="Value">The payload value for the record.</param>
/// <param name="ValueType">The semantic type of <paramref name="Value"/>; controls wire encoding.</param>
public sealed record TlvRecord(
    TlvRecordType Type,
    int Identifier,
    PayloadValue Value,
    PayloadType ValueType);
