namespace Tinkwell.Modbus;

/// <summary>
/// Specifies how to decode raw 16-bit Modbus registers into typed values.
/// </summary>
/// <remarks>
/// <para>The Modbus specification (<em>MODBUS Application Protocol V1.1b3</em>,
/// Section 4.2) defines registers as 16-bit quantities but does not prescribe
/// how multi-register values are encoded. Byte and word ordering varies by
/// device manufacturer. This enum covers the most common conventions.</para>
/// <para>Byte order labels use the convention where A is the most significant byte
/// and D is the least significant: ABCD = big-endian, DCBA = little-endian,
/// BADC = word-swapped (mid-big-endian).</para>
/// </remarks>
public enum ModbusDataType
{
    /// <summary>
    /// Signed 16-bit integer. Uses 1 register.
    /// The register value is interpreted as a two's complement signed integer.
    /// </summary>
    Int16,

    /// <summary>
    /// Unsigned 16-bit integer. Uses 1 register.
    /// The register value is used directly without sign interpretation.
    /// </summary>
    UInt16,

    /// <summary>
    /// Signed 32-bit integer, big-endian (ABCD). Uses 2 registers.
    /// Register N holds the high word, register N+1 the low word.
    /// </summary>
    Int32BigEndian,

    /// <summary>
    /// Signed 32-bit integer, little-endian (DCBA). Uses 2 registers.
    /// Register N holds the low word, register N+1 the high word.
    /// </summary>
    Int32LittleEndian,

    /// <summary>
    /// Unsigned 32-bit integer, big-endian (ABCD). Uses 2 registers.
    /// Register N holds the high word, register N+1 the low word.
    /// </summary>
    UInt32BigEndian,

    /// <summary>
    /// Unsigned 32-bit integer, little-endian (DCBA). Uses 2 registers.
    /// Register N holds the low word, register N+1 the high word.
    /// </summary>
    UInt32LittleEndian,

    /// <summary>
    /// IEEE 754 single-precision float, big-endian (ABCD). Uses 2 registers.
    /// Register N holds the high word, register N+1 the low word.
    /// This is the most common byte order for floating-point values in
    /// Modbus devices.
    /// </summary>
    Float32BigEndian,

    /// <summary>
    /// IEEE 754 single-precision float, little-endian (DCBA). Uses 2 registers.
    /// Register N holds the low word, register N+1 the high word.
    /// </summary>
    Float32LittleEndian,

    /// <summary>
    /// IEEE 754 single-precision float, word-swapped / mid-big-endian (BADC).
    /// Uses 2 registers. Words are swapped relative to <see cref="Float32BigEndian"/>:
    /// bytes within each word are big-endian, but the word order is reversed.
    /// Common in Schneider Electric / Modicon PLCs.
    /// </summary>
    Float32WordSwapped,
}
