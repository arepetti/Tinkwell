using Tinkwell.Coap;
using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class PayloadCodecTests
{
    [Fact]
    public void DecodeSingleResource_TextPlain_Float()
    {
        var payload = "23.5"u8.ToArray();
        var value = PayloadCodec.DecodeSingleResource(
            payload, CoapContentFormat.TextPlain, PayloadType.Float);

        Assert.Equal(23.5, value.AsDouble());
    }

    [Fact]
    public void DecodeSingleResource_TextPlain_Integer()
    {
        var payload = "42"u8.ToArray();
        var value = PayloadCodec.DecodeSingleResource(
            payload, CoapContentFormat.TextPlain, PayloadType.Integer);

        Assert.Equal(42, value.AsLong());
    }

    [Fact]
    public void DecodeSingleResource_TextPlain_Boolean()
    {
        var payload = "true"u8.ToArray();
        var value = PayloadCodec.DecodeSingleResource(
            payload, CoapContentFormat.TextPlain, PayloadType.Boolean);

        Assert.True(value.AsBoolean());
    }

    [Fact]
    public void DecodeSingleResource_TextPlain_String()
    {
        var payload = "hello world"u8.ToArray();
        var value = PayloadCodec.DecodeSingleResource(
            payload, CoapContentFormat.TextPlain, PayloadType.String);

        Assert.Equal("hello world", value.AsString());
    }

    [Fact]
    public void DecodeSingleResource_TextPlain_UnparseableFloat_FallbackToString()
    {
        var payload = "not-a-number"u8.ToArray();
        var value = PayloadCodec.DecodeSingleResource(
            payload, CoapContentFormat.TextPlain, PayloadType.Float);

        Assert.Equal(PayloadType.String, value.Type);
        Assert.Equal("not-a-number", value.AsString());
    }

    [Fact]
    public void DecodeSingleResource_Tlv_Float()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5700,
            PayloadValue.FromFloat(23.5), PayloadType.Float);
        var tlv = TlvEncoder.EncodeSingle(record);

        var value = PayloadCodec.DecodeSingleResource(
            tlv, CoapContentFormat.ApplicationLwm2mTlv, PayloadType.Float);

        Assert.Equal(23.5, value.AsDouble());
    }

    [Fact]
    public void DecodeSingleResource_SenmlJson_Float()
    {
        var records = new List<SenmlRecord>
        {
            new(5700, PayloadValue.FromFloat(23.5)),
        };
        var json = SenmlJsonCodec.Encode(3303, 0, records);

        var value = PayloadCodec.DecodeSingleResource(
            json, CoapContentFormat.ApplicationSenmlJson);

        Assert.Equal(23.5, value.AsDouble());
    }

    [Fact]
    public void DecodeSingleResource_OctetStream_ReturnsOpaque()
    {
        var data = new byte[] { 0xDE, 0xAD };
        var value = PayloadCodec.DecodeSingleResource(
            data, CoapContentFormat.ApplicationOctetStream);

        Assert.Equal(PayloadType.Opaque, value.Type);
    }

    [Fact]
    public void DecodeSingleResource_UnsupportedFormat_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            PayloadCodec.DecodeSingleResource([], (CoapContentFormat)99999));
    }

    [Fact]
    public void IsSupported_KnownFormats_ReturnsTrue()
    {
        Assert.True(PayloadCodec.IsSupported(CoapContentFormat.TextPlain));
        Assert.True(PayloadCodec.IsSupported(CoapContentFormat.ApplicationLwm2mTlv));
        Assert.True(PayloadCodec.IsSupported(CoapContentFormat.ApplicationSenmlJson));
    }

    [Fact]
    public void IsSupported_UnknownFormat_ReturnsFalse()
    {
        Assert.False(PayloadCodec.IsSupported((CoapContentFormat)99999));
        Assert.False(PayloadCodec.IsSupported(CoapContentFormat.ApplicationSenmlCbor));
    }
}
