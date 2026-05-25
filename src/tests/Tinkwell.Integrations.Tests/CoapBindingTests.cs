using System.Text;
using Tinkwell.Integration.Coap;

namespace Tinkwell.Integrations.Tests;

public class CoapBindingTests
{
    [Fact]
    public void ExtractPayload_NoToken_NoOptions_PayloadMarkerAndBody()
    {
        // Ver 1, TKL=0, 2.05, message id 0x0001, then 0xFF + "Hi"
        var data = new byte[] { 0x40, 0x45, 0x00, 0x01, 0xFF, 0x48, 0x69 };
        var payload = CoapBinding.ExtractPayload(data);
        Assert.Equal("Hi", Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void ExtractPayload_OneUriPath_ThenPayload()
    {
        // TKL=0, one option delta=11 len=1 'a', then 0xFF + "ok"
        var data = new byte[] { 0x40, 0x45, 0x00, 0x00, 0xB1, 0x61, 0xFF, 0x6F, 0x6B };
        var payload = CoapBinding.ExtractPayload(data);
        Assert.Equal("ok", Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void ExtractPayload_TwoPathSegments_ThenPayload()
    {
        // First: delta 11, len 1 'a' → 0xB1, 0x61
        // Second: from opt 11 to 22, delta 11, len 1 'b' → 0xB1, 0x62
        var data = new byte[] { 0x40, 0x45, 0, 0, 0xB1, 0x61, 0xB1, 0x62, 0xFF, 0x7A };
        var payload = CoapBinding.ExtractPayload(data);
        Assert.Equal("z", Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void ExtractPayload_NoPayloadMarker_ReturnsEmpty()
    {
        var data = new byte[] { 0x40, 0x45, 0, 0, 0xB1, 0x61 };
        var payload = CoapBinding.ExtractPayload(data);
        Assert.Empty(payload);
    }

    [Fact]
    public void ExtractPayload_ExtendedDelta13_EncodesAndReadsOption()
    {
        // At offset 4: (13<<4)|1 = 0xD1, ext delta byte 0 → option number 0+13=13, length 1, value 0x61
        // Then 0xFF + "x"
        var data = new byte[] { 0x40, 0x45, 0, 0, 0xD1, 0x00, 0x61, 0xFF, 0x78 };
        var payload = CoapBinding.ExtractPayload(data);
        Assert.Equal("x", Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void ExtractPayload_ExtendedLength13_14ValueBytes()
    {
        // Option number 11, length 14: nibble 13 + byte 1 → 13+1=14 bytes value
        var value = new byte[14];
        value[0] = 0xAB;
        var data = new List<byte> { 0x40, 0x45, 0, 0, 0xBD, 0x01 };
        data.AddRange(value);
        data.Add(0xFF);
        data.AddRange(Encoding.UTF8.GetBytes("tail"));
        var payload = CoapBinding.ExtractPayload(data.ToArray());
        Assert.Equal("tail", Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void ExtractPayload_ExtendedLength14_LargeValue()
    {
        // delta 11, length 270: nibble 14 + 2 bytes (270-269) = 1 as uint16 be → 0x0001 + 269 = 270
        var value = new byte[270];
        Array.Fill(value, (byte)0xCC);
        var data = new List<byte> { 0x40, 0x45, 0, 0, 0xBE, 0x00, 0x01 };
        data.AddRange(value);
        data.Add(0xFF);
        data.Add(0x01);
        var payload = CoapBinding.ExtractPayload(data.ToArray());
        Assert.Equal([0x01], payload);
    }

    [Fact]
    public void ExtractPayload_DeltaNibble15_ReturnsEmpty()
    {
        var data = new byte[] { 0x40, 0x45, 0, 0, 0xF0, 0xFF, 0x01 };
        var payload = CoapBinding.ExtractPayload(data);
        Assert.Empty(payload);
    }

    [Fact]
    public void ExtractPayload_TruncatedOptionValue_ReturnsEmpty()
    {
        // length nibble 12 → 12 bytes value but only 1 byte after header
        var data = new byte[] { 0x40, 0x45, 0, 0, 0xBC, 0x00 };
        var payload = CoapBinding.ExtractPayload(data);
        Assert.Empty(payload);
    }

    [Fact]
    public void ExtractPayload_EmptyBuffer_ReturnsEmpty()
    {
        var payload = CoapBinding.ExtractPayload([]);
        Assert.Empty(payload);
    }

    [Fact]
    public void ExtractPayload_ShortBuffer_ReturnsEmpty()
    {
        var payload = CoapBinding.ExtractPayload([0x40, 0x45, 0x00]);
        Assert.Empty(payload);
    }

    [Fact]
    public void TryReadOptionValue_SmallNibble_ReturnsValueWithoutAdvance()
    {
        var data = new byte[] { 0x00 };
        var off = 0;
        Assert.True(CoapBinding.TryReadOptionValue(5, data, ref off, out var v));
        Assert.Equal(5, v);
        Assert.Equal(0, off);
    }

    [Theory]
    [InlineData(13, new byte[] { 0x02 }, 0, 15)] // 2+13=15
    [InlineData(14, new byte[] { 0x00, 0x0A }, 0, 279)] // 10+269
    public void TryReadOptionValue_Extended(int nibble, byte[] rest, int startOffset, int expected)
    {
        var data = new byte[2 + rest.Length];
        var off = startOffset;
        Array.Copy(rest, 0, data, off, rest.Length);
        Assert.True(CoapBinding.TryReadOptionValue(nibble, data, ref off, out var v));
        Assert.Equal(expected, v);
    }

    [Fact]
    public void TryReadOptionValue_Nibble15_False()
    {
        var data = new byte[] { 0x00 };
        var off = 0;
        Assert.False(CoapBinding.TryReadOptionValue(15, data, ref off, out _));
    }

    [Fact]
    public void TryReadOptionValue_Nibble13_MissingByte_False()
    {
        var data = new byte[] { 0x00 };
        var off = 1; // past end
        Assert.False(CoapBinding.TryReadOptionValue(13, data, ref off, out _));
    }

    [Fact]
    public void TryReadOptionValue_Nibble14_OneByteShort_False()
    {
        var data = new byte[] { 0x00, 0x01 };
        var off = 1; // need 2 bytes at offset 1, only 1 available
        Assert.False(CoapBinding.TryReadOptionValue(14, data, ref off, out _));
    }
}
