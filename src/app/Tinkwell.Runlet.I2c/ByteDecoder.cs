using System.Buffers.Binary;
using Tinkwell.Runlet.I2c.Configuration;

namespace Tinkwell.Runlet.I2c;

/// <summary>
/// Decodes raw byte buffers read from I2C devices into typed numeric values.
/// </summary>
internal static class ByteDecoder
{
    public static double Decode(ReadOnlySpan<byte> buffer, I2cDataType type, double scale)
    {
        int required = RequiredBytes(type);
        if (buffer.Length < required)
        {
            throw new ArgumentException(
                $"Buffer too short for {type}: expected at least {required} byte(s), got {buffer.Length}.",
                nameof(buffer));
        }

        double raw = type switch
        {
            I2cDataType.Int8 => (sbyte)buffer[0],
            I2cDataType.UInt8 => buffer[0],
            I2cDataType.Int16BE => BinaryPrimitives.ReadInt16BigEndian(buffer),
            I2cDataType.Int16LE => BinaryPrimitives.ReadInt16LittleEndian(buffer),
            I2cDataType.UInt16BE => BinaryPrimitives.ReadUInt16BigEndian(buffer),
            I2cDataType.UInt16LE => BinaryPrimitives.ReadUInt16LittleEndian(buffer),
            I2cDataType.Int32BE => BinaryPrimitives.ReadInt32BigEndian(buffer),
            I2cDataType.Int32LE => BinaryPrimitives.ReadInt32LittleEndian(buffer),
            I2cDataType.Float32BE => ReadFloat32BigEndian(buffer),
            I2cDataType.Float32LE => BinaryPrimitives.ReadSingleLittleEndian(buffer),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        return raw * scale;
    }

    /// <summary>
    /// Returns the minimum number of bytes required to decode the given data type.
    /// </summary>
    public static int RequiredBytes(I2cDataType type) => type switch
    {
        I2cDataType.Int8 or I2cDataType.UInt8 => 1,
        I2cDataType.Int16BE or I2cDataType.Int16LE
            or I2cDataType.UInt16BE or I2cDataType.UInt16LE => 2,
        I2cDataType.Int32BE or I2cDataType.Int32LE
            or I2cDataType.Float32BE or I2cDataType.Float32LE => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static float ReadFloat32BigEndian(ReadOnlySpan<byte> buffer)
    {
        var intBits = BinaryPrimitives.ReadInt32BigEndian(buffer);
        return BitConverter.Int32BitsToSingle(intBits);
    }
}
