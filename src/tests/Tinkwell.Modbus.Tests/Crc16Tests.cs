using System.Text;

namespace Tinkwell.Modbus.Tests;

public class Crc16Tests
{
    [Fact]
    public void Empty_ReturnsInitialValue()
    {
        Assert.Equal(0xFFFF, Crc16.Compute(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void CatalogCheckValue_123456789()
    {
        // CRC-16/MODBUS "check" value from the CRC RevEng catalog:
        // the string "123456789" must produce 0x4B37.
        var data = Encoding.ASCII.GetBytes("123456789");
        Assert.Equal(0x4B37, Crc16.Compute(data));
    }

    [Fact]
    public void AppendingCrcLittleEndian_MakesTotalCrcZero()
    {
        ReadOnlySpan<byte> message = new byte[] { 0x11, 0x03, 0x00, 0x6B, 0x00, 0x03 };
        var crc = Crc16.Compute(message);

        Span<byte> framed = stackalloc byte[message.Length + 2];
        message.CopyTo(framed);
        framed[^2] = (byte)(crc & 0xFF);
        framed[^1] = (byte)(crc >> 8);

        Assert.Equal(0, Crc16.Compute(framed));
    }
}
