using Tinkwell.Coap;
using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class PayloadCodecEdgeCaseTests
{
    [Fact]
    public void DecodeSingleResource_TextPlain_WhitespaceTrimed()
    {
        var payload = "  23.5  "u8.ToArray();
        var value = PayloadCodec.DecodeSingleResource(
            payload, CoapContentFormat.TextPlain, PayloadType.Float);
        Assert.Equal(23.5, value.AsDouble());
    }

    [Fact]
    public void DecodeSingleResource_TextPlain_NegativeFloat()
    {
        var payload = "-10.5"u8.ToArray();
        var value = PayloadCodec.DecodeSingleResource(
            payload, CoapContentFormat.TextPlain, PayloadType.Float);
        Assert.Equal(-10.5, value.AsDouble());
    }

    [Fact]
    public void DecodeSingleResource_TextPlain_NegativeInteger()
    {
        var payload = "-42"u8.ToArray();
        var value = PayloadCodec.DecodeSingleResource(
            payload, CoapContentFormat.TextPlain, PayloadType.Integer);
        Assert.Equal(-42, value.AsLong());
    }

    [Fact]
    public void DecodeSingleResource_TextPlain_BooleanFalse()
    {
        var payload = "false"u8.ToArray();
        var value = PayloadCodec.DecodeSingleResource(
            payload, CoapContentFormat.TextPlain, PayloadType.Boolean);
        Assert.False(value.AsBoolean());
    }

    [Fact]
    public void DecodeSingleResource_TextPlain_EmptyPayload_FallbackToString()
    {
        var value = PayloadCodec.DecodeSingleResource(
            ""u8.ToArray(), CoapContentFormat.TextPlain, PayloadType.Float);
        Assert.Equal(PayloadType.String, value.Type);
    }

    [Fact]
    public void DecodeSingleResource_TlvEmpty_ReturnsEmpty()
    {
        var value = PayloadCodec.DecodeSingleResource(
            ReadOnlySpan<byte>.Empty, CoapContentFormat.ApplicationLwm2mTlv);
        Assert.Equal(PayloadType.None, value.Type);
    }

    [Fact]
    public void DecodeSingleResource_SenmlJsonEmpty_ReturnsEmpty()
    {
        var json = "[]"u8.ToArray();
        var value = PayloadCodec.DecodeSingleResource(
            json, CoapContentFormat.ApplicationSenmlJson);
        Assert.Equal(PayloadType.None, value.Type);
    }

    [Fact]
    public void DecodeSingleResource_OctetStream_EmptyPayload()
    {
        var value = PayloadCodec.DecodeSingleResource(
            ReadOnlySpan<byte>.Empty, CoapContentFormat.ApplicationOctetStream);
        Assert.Equal(PayloadType.Opaque, value.Type);
        Assert.Empty((byte[])value.RawValue!);
    }

    [Theory]
    [InlineData(CoapContentFormat.TextPlain)]
    [InlineData(CoapContentFormat.ApplicationOctetStream)]
    [InlineData(CoapContentFormat.ApplicationLwm2mTlv)]
    [InlineData(CoapContentFormat.ApplicationSenmlJson)]
    public void IsSupported_AllSupportedFormats(CoapContentFormat format)
    {
        Assert.True(PayloadCodec.IsSupported(format));
    }

    [Theory]
    [InlineData(CoapContentFormat.ApplicationCbor)]
    [InlineData(CoapContentFormat.ApplicationSenmlCbor)]
    [InlineData(CoapContentFormat.ApplicationLwm2mJson)]
    [InlineData((CoapContentFormat)(-1))]
    [InlineData((CoapContentFormat)int.MaxValue)]
    public void IsSupported_UnsupportedFormats(CoapContentFormat format)
    {
        Assert.False(PayloadCodec.IsSupported(format));
    }
}
