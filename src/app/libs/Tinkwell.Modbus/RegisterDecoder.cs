using System.Buffers.Binary;

namespace Tinkwell.Modbus;

/// <summary>
/// Decodes raw 16-bit Modbus register values into typed .NET primitives.
/// </summary>
/// <remarks>
/// <para>Modbus registers are 16-bit unsigned quantities transmitted in big-endian
/// (network) byte order per <em>MODBUS Application Protocol V1.1b3</em>, Section 4.2.
/// Multi-register values (32-bit integers and IEEE 754 floats) require 2 consecutive
/// registers, but the byte and word ordering is device-specific.</para>
/// <para>This class provides individual decode methods for each byte order variant,
/// as well as a convenience <see cref="Decode"/> method driven by <see cref="ModbusDataType"/>.</para>
/// </remarks>
/// <example>
/// <para>After reading the correct number of registers, decode using the method that matches the device manual (or <see cref="Decode"/> with <see cref="ModbusDataType"/>).</para>
/// <code language="csharp">
/// int n = RegisterDecoder.RegisterCount(ModbusDataType.Float32BigEndian);
/// ushort[] regs = await client.ReadHoldingRegistersAsync(1, 0x0000, (ushort)n);
/// double rpm = RegisterDecoder.Decode(regs, ModbusDataType.Float32BigEndian, scale: 1.0);
/// </code>
/// </example>
public static class RegisterDecoder
{
    /// <summary>
    /// Interprets a single register as a signed 16-bit integer (two's complement).
    /// </summary>
    /// <example>
    /// <para>When the device stores temperature in tenths of a degree, scale after decoding to degrees.</para>
    /// <code language="csharp">
    /// short rawTenths = RegisterDecoder.ToInt16(0x00D7); // 215
    /// double celsius = rawTenths * 0.1; // 21.5
    /// </code>
    /// </example>
    /// <param name="register">The raw register value.</param>
    /// <returns>The signed 16-bit value.</returns>
    public static short ToInt16(ushort register) => (short)register;

    /// <summary>
    /// Returns a single register as an unsigned 16-bit integer (identity conversion).
    /// </summary>
    /// <example>
    /// <para>Example: <c>RegisterDecoder.ToUInt16(0x0FF0)</c> is <c>4080</c>.</para>
    /// <code language="csharp">
    /// ushort u = RegisterDecoder.ToUInt16(0x0FF0); // 4080
    /// </code>
    /// </example>
    /// <param name="register">The raw register value.</param>
    /// <returns>The unsigned 16-bit value.</returns>
    public static ushort ToUInt16(ushort register) => register;

    /// <summary>
    /// Decodes two registers as a signed 32-bit integer in big-endian (ABCD) word order.
    /// </summary>
    /// <example>
    /// <para>Example: <c>ToInt32BigEndian(0x0001, 0x0002)</c> is <c>65538</c>.</para>
    /// <code language="csharp">
    /// int v = RegisterDecoder.ToInt32BigEndian(0x0001, 0x0002); // 65538
    /// </code>
    /// </example>
    /// <param name="hi">High word — register at address N.</param>
    /// <param name="lo">Low word — register at address N+1.</param>
    /// <returns>The signed 32-bit value.</returns>
    public static int ToInt32BigEndian(ushort hi, ushort lo) => (hi << 16) | lo;

    /// <summary>
    /// Decodes two registers as a signed 32-bit integer in little-endian (DCBA) word order.
    /// </summary>
    /// <example>
    /// <para>Example: <c>ToInt32LittleEndian(0x0002, 0x0001)</c> is <c>65538</c> (word order reversed vs. big-endian).</para>
    /// <code language="csharp">
    /// int v = RegisterDecoder.ToInt32LittleEndian(0x0002, 0x0001); // 65538
    /// </code>
    /// </example>
    /// <param name="lo">Low word — register at address N.</param>
    /// <param name="hi">High word — register at address N+1.</param>
    /// <returns>The signed 32-bit value.</returns>
    public static int ToInt32LittleEndian(ushort lo, ushort hi) => (hi << 16) | lo;

    /// <summary>
    /// Decodes two registers as an unsigned 32-bit integer in big-endian (ABCD) word order.
    /// </summary>
    /// <example>
    /// <para>Example: <c>ToUInt32BigEndian(0x0000, 0x00C8)</c> is <c>200</c>.</para>
    /// <code language="csharp">
    /// uint u = RegisterDecoder.ToUInt32BigEndian(0x0000, 0x00C8); // 200
    /// </code>
    /// </example>
    /// <param name="hi">High word — register at address N.</param>
    /// <param name="lo">Low word — register at address N+1.</param>
    /// <returns>The unsigned 32-bit value.</returns>
    public static uint ToUInt32BigEndian(ushort hi, ushort lo) => ((uint)hi << 16) | lo;

    /// <summary>
    /// Decodes two registers as an unsigned 32-bit integer in little-endian (DCBA) word order.
    /// </summary>
    /// <example>
    /// <para>Example: <c>ToUInt32LittleEndian(0x00C8, 0x0000)</c> is <c>200</c>.</para>
    /// <code language="csharp">
    /// uint u = RegisterDecoder.ToUInt32LittleEndian(0x00C8, 0x0000); // 200
    /// </code>
    /// </example>
    /// <param name="lo">Low word — register at address N.</param>
    /// <param name="hi">High word — register at address N+1.</param>
    /// <returns>The unsigned 32-bit value.</returns>
    public static uint ToUInt32LittleEndian(ushort lo, ushort hi) => ((uint)hi << 16) | lo;

    /// <summary>
    /// Decodes two registers as an IEEE 754 single-precision float in big-endian (ABCD)
    /// word order. This is the most common float encoding in Modbus devices.
    /// </summary>
    /// <example>
    /// <para>Two holding registers at <c>N</c> and <c>N+1</c> often encode a sensor such as bearing vibration in m/s².</para>
    /// <code language="csharp">
    /// float vibration = RegisterDecoder.ToFloat32BigEndian(regs[0], regs[1]);
    /// </code>
    /// </example>
    /// <param name="hi">High word — register at address N.</param>
    /// <param name="lo">Low word — register at address N+1.</param>
    /// <returns>The decoded <see langword="float"/> value.</returns>
    public static float ToFloat32BigEndian(ushort hi, ushort lo)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(buf, hi);
        BinaryPrimitives.WriteUInt16BigEndian(buf[2..], lo);
        return BinaryPrimitives.ReadSingleBigEndian(buf);
    }

    /// <summary>
    /// Decodes two registers as an IEEE 754 single-precision float in little-endian (DCBA)
    /// word order.
    /// </summary>
    /// <example>
    /// <para>Example: with words <c>0x447A</c> and <c>0x0000</c>, the result is the IEEE float from DCBA order.</para>
    /// <code language="csharp">
    /// float f = RegisterDecoder.ToFloat32LittleEndian(0x447A, 0x0000);
    /// </code>
    /// </example>
    /// <param name="lo">Low word — register at address N.</param>
    /// <param name="hi">High word — register at address N+1.</param>
    /// <returns>The decoded <see langword="float"/> value.</returns>
    public static float ToFloat32LittleEndian(ushort lo, ushort hi)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(buf, hi);
        BinaryPrimitives.WriteUInt16BigEndian(buf[2..], lo);
        return BinaryPrimitives.ReadSingleBigEndian(buf);
    }

    /// <summary>
    /// Decodes two registers as an IEEE 754 single-precision float in word-swapped (BADC)
    /// order. Common in Schneider Electric / Modicon PLCs.
    /// </summary>
    /// <remarks>
    /// In BADC order the two 16-bit words are swapped relative to big-endian (ABCD),
    /// but bytes within each word remain in big-endian order.
    /// </remarks>
    /// <example>
    /// <para>Many Schneider / Modicon devices expose IEEE floats in word-swapped (BADC) order; pass registers in address order.</para>
    /// <code language="csharp">
    /// float powerKw = RegisterDecoder.ToFloat32WordSwapped(regs[0], regs[1]);
    /// </code>
    /// </example>
    /// <param name="hi">First register at address N (contains bytes B, A).</param>
    /// <param name="lo">Second register at address N+1 (contains bytes D, C).</param>
    /// <returns>The decoded <see langword="float"/> value.</returns>
    public static float ToFloat32WordSwapped(ushort hi, ushort lo)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(buf, lo);
        BinaryPrimitives.WriteUInt16BigEndian(buf[2..], hi);
        return BinaryPrimitives.ReadSingleBigEndian(buf);
    }

    /// <summary>
    /// Decodes one or two registers according to the specified <see cref="ModbusDataType"/>,
    /// optionally applying a scale factor.
    /// </summary>
    /// <example>
    /// <para>Use <see cref="ModbusDataType"/> and a scale to match the instrument manual (e.g. tenths, millimeters).</para>
    /// <code language="csharp">
    /// double celsius = RegisterDecoder.Decode(
    ///     registers, ModbusDataType.Int16, scale: 0.1);
    /// </code>
    /// </example>
    /// <param name="registers">
    /// The raw register array. Must contain at least <see cref="RegisterCount"/> elements
    /// for the given <paramref name="dataType"/>.
    /// </param>
    /// <param name="dataType">Determines how to interpret the register bytes.</param>
    /// <param name="scale">
    /// Multiplier applied after decoding. Useful for sensors that report in fixed-point
    /// (e.g. <c>scale: 0.1</c> when the device reports temperature in tenths of a degree).
    /// Defaults to 1.0.
    /// </param>
    /// <returns>The decoded value as a <see langword="double"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dataType"/> is not a valid enum value.</exception>
    /// <exception cref="IndexOutOfRangeException"><paramref name="registers"/> is too short for the data type.</exception>
    public static double Decode(ushort[] registers, ModbusDataType dataType, double scale = 1.0)
    {
        var raw = dataType switch
        {
            ModbusDataType.Int16 => (double)ToInt16(registers[0]),
            ModbusDataType.UInt16 => (double)ToUInt16(registers[0]),
            ModbusDataType.Int32BigEndian => (double)ToInt32BigEndian(registers[0], registers[1]),
            ModbusDataType.Int32LittleEndian => (double)ToInt32LittleEndian(registers[0], registers[1]),
            ModbusDataType.UInt32BigEndian => (double)ToUInt32BigEndian(registers[0], registers[1]),
            ModbusDataType.UInt32LittleEndian => (double)ToUInt32LittleEndian(registers[0], registers[1]),
            ModbusDataType.Float32BigEndian => (double)ToFloat32BigEndian(registers[0], registers[1]),
            ModbusDataType.Float32LittleEndian => (double)ToFloat32LittleEndian(registers[0], registers[1]),
            ModbusDataType.Float32WordSwapped => (double)ToFloat32WordSwapped(registers[0], registers[1]),
            _ => throw new ArgumentOutOfRangeException(nameof(dataType)),
        };

        return raw * scale;
    }

    /// <summary>
    /// Returns the number of 16-bit registers required for the given data type.
    /// </summary>
    /// <example>
    /// <para>Pass the count to <see cref="IModbusClient.ReadHoldingRegistersAsync"/> (or <see cref="IModbusClient.ReadInputRegistersAsync"/>) so the buffer is long enough for <see cref="Decode"/>.</para>
    /// <code language="csharp">
    /// ushort n = (ushort)RegisterDecoder.RegisterCount(ModbusDataType.Float32WordSwapped);
    /// ushort[] regs = await client.ReadHoldingRegistersAsync(1, 0x1000, n);
    /// </code>
    /// </example>
    /// <param name="dataType">The data type to query.</param>
    /// <returns>1 for 16-bit types, 2 for 32-bit types.</returns>
    public static int RegisterCount(ModbusDataType dataType) => dataType switch
    {
        ModbusDataType.Int16 or ModbusDataType.UInt16 => 1,
        _ => 2,
    };
}
