namespace Tinkwell.Modbus;

/// <summary>
/// CRC-16/Modbus checksum used by the RTU framing mode.
/// </summary>
/// <remarks>
/// <para>Polynomial: 0xA001 (bit-reversed form of 0x8005).
/// Initial value: 0xFFFF. No final XOR.</para>
/// <para>Defined in <em>MODBUS over Serial Line Specification and Implementation
/// Guide V1.02</em> (Modbus.org, 2006), Section 2.5.1.2 — CRC Generation.</para>
/// <para>Uses a pre-computed 256-entry lookup table for performance.</para>
/// </remarks>
internal static class Crc16
{
    private static readonly ushort[] Table = BuildTable();

    /// <summary>
    /// Computes the CRC-16/Modbus checksum over the given data.
    /// </summary>
    /// <param name="data">The bytes to checksum (the frame without the CRC field).</param>
    /// <returns>The 16-bit CRC. Transmitted low byte first per the specification.</returns>
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc = (ushort)((crc >> 8) ^ Table[(crc ^ b) & 0xFF]);
        }
        return crc;
    }

    private static ushort[] BuildTable()
    {
        var table = new ushort[256];
        for (int i=0; i < 256; ++i)
        {
            ushort crc = (ushort)i;
            for (int j=0; j < 8; ++j)
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
            table[i] = crc;
        }
        return table;
    }
}
