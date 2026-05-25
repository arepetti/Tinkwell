using Tinkwell.Coap;

namespace Tinkwell.Coap.Tests;

public class CoapOptionTests
{
    [Fact]
    public void AsUInt_EmptyValue_ReturnsZero()
    {
        var opt = new CoapOption(CoapOptionNumber.Observe, []);
        Assert.Equal(0, opt.AsUInt());
    }

    [Fact]
    public void AsUInt_OneByte()
    {
        Assert.Equal(0, new CoapOption(6, [0]).AsUInt());
        Assert.Equal(127, new CoapOption(6, [127]).AsUInt());
        Assert.Equal(255, new CoapOption(6, [255]).AsUInt());
    }

    [Fact]
    public void AsUInt_TwoBytes_BigEndian()
    {
        Assert.Equal(256, new CoapOption(6, [1, 0]).AsUInt());
        Assert.Equal(0x0102, new CoapOption(6, [1, 2]).AsUInt());
        Assert.Equal(0xFFFF, new CoapOption(6, [0xFF, 0xFF]).AsUInt());
    }

    [Fact]
    public void AsUInt_ThreeBytes_BigEndian()
    {
        Assert.Equal(0x010203, new CoapOption(6, [1, 2, 3]).AsUInt());
        Assert.Equal(0xFFFFFF, new CoapOption(6, [0xFF, 0xFF, 0xFF]).AsUInt());
    }

    [Fact]
    public void AsUInt_FourBytes_Decoded()
    {
        // RFC 7252 §3.2 / RFC 7959 §4 allow up to 4 bytes for uint-encoded options (e.g. Size1).
        Assert.Equal(0x01020304, new CoapOption(60, [1, 2, 3, 4]).AsUInt());
        Assert.Equal(int.MaxValue, new CoapOption(60, [0x7F, 0xFF, 0xFF, 0xFF]).AsUInt());
    }

    [Fact]
    public void AsUInt_FourBytes_OverflowingInt_Throws()
    {
        // Values that don't fit in a signed int are surfaced as OverflowException
        // so callers aren't handed a negative number by mistake.
        Assert.Throws<OverflowException>(() =>
            new CoapOption(60, [0x80, 0, 0, 0]).AsUInt());
    }

    [Fact]
    public void AsUInt_FiveBytes_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new CoapOption(6, [1, 2, 3, 4, 5]).AsUInt());
    }

    [Fact]
    public void AsString_Utf8()
    {
        var opt = new CoapOption(CoapOptionNumber.UriPath, "sensors"u8.ToArray());
        Assert.Equal("sensors", opt.AsString());
    }

    [Fact]
    public void AsString_Empty()
    {
        var opt = new CoapOption(CoapOptionNumber.UriPath, []);
        Assert.Equal("", opt.AsString());
    }

    [Fact]
    public void AsString_Unicode()
    {
        var opt = new CoapOption(CoapOptionNumber.UriPath,
            System.Text.Encoding.UTF8.GetBytes("\u00B0C"));
        Assert.Equal("\u00B0C", opt.AsString());
    }

    [Fact]
    public void RecordEquality_SameNumberAndValue()
    {
        var a = new CoapOption(11, [0x01, 0x02]);
        var b = new CoapOption(11, [0x01, 0x02]);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void OptionNumber_Constants_CorrectValues()
    {
        Assert.Equal(6, CoapOptionNumber.Observe);
        Assert.Equal(11, CoapOptionNumber.UriPath);
        Assert.Equal(12, CoapOptionNumber.ContentFormat);
        Assert.Equal(15, CoapOptionNumber.UriQuery);
        Assert.Equal(17, CoapOptionNumber.Accept);
    }
}
