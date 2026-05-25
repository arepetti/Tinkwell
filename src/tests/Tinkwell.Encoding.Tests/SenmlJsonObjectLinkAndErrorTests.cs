using SysEncoding = System.Text.Encoding;
using System.Text.Json;
using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class SenmlJsonObjectLinkAndErrorTests
{
    [Fact]
    public void Encode_ObjectLink_EmitsVloField()
    {
        var records = new List<SenmlRecord>
        {
            new(5560, PayloadValue.FromObjectLink(3303, 0)),
        };

        var json = SenmlJsonCodec.Encode(3300, 0, records);
        var text = SysEncoding.UTF8.GetString(json);

        var doc = JsonDocument.Parse(text);
        var first = doc.RootElement[0];
        Assert.Equal("3303:0", first.GetProperty("vlo").GetString());
    }

    [Fact]
    public void Decode_VloField_ReturnsObjectLinkValue()
    {
        var json = SysEncoding.UTF8.GetBytes(
            "[{\"bn\":\"/3300/0/\",\"n\":\"5560\",\"vlo\":\"3303:0\"}]");

        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Single(decoded);
        Assert.Equal(PayloadType.ObjectLink, decoded[0].Value.Type);
        Assert.Equal(new ObjectLink(3303, 0), decoded[0].Value.AsObjectLink());
    }

    [Fact]
    public void Roundtrip_ObjectLink()
    {
        var original = new List<SenmlRecord>
        {
            new(5560, PayloadValue.FromObjectLink(3303, 0)),
        };

        var json = SenmlJsonCodec.Encode(3300, 0, original);
        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Equal(new ObjectLink(3303, 0), decoded[0].Value.AsObjectLink());
    }

    [Fact]
    public void Decode_VloFieldWithBadString_ThrowsFormatException()
    {
        var json = SysEncoding.UTF8.GetBytes("[{\"n\":\"x\",\"vlo\":\"not-a-link\"}]");

        Assert.Throws<FormatException>(() => SenmlJsonCodec.Decode(json));
    }

    [Fact]
    public void Encode_NoneValue_OmitsValueFields()
    {
        var records = new List<SenmlRecord>
        {
            new(5700, PayloadValue.Empty),
        };

        var json = SenmlJsonCodec.Encode(3303, 0, records);
        var doc = JsonDocument.Parse(json);
        var first = doc.RootElement[0];

        Assert.False(first.TryGetProperty("v", out _));
        Assert.False(first.TryGetProperty("vs", out _));
        Assert.False(first.TryGetProperty("vb", out _));
        Assert.False(first.TryGetProperty("vd", out _));
        Assert.False(first.TryGetProperty("vlo", out _));
        Assert.Equal("5700", first.GetProperty("n").GetString());
    }

    [Fact]
    public void Roundtrip_NoneValue()
    {
        var original = new List<SenmlRecord> { new(5700, PayloadValue.Empty) };

        var json = SenmlJsonCodec.Encode(3303, 0, original);
        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Single(decoded);
        Assert.Equal(PayloadType.None, decoded[0].Value.Type);
    }

    [Fact]
    public void Decode_VStringWhereNumberExpected_ThrowsFormatException()
    {
        var json = SysEncoding.UTF8.GetBytes("[{\"n\":\"5700\",\"v\":\"23.5\"}]");

        var ex = Assert.Throws<FormatException>(() => SenmlJsonCodec.Decode(json));
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void Decode_VbWithNumber_ThrowsFormatException()
    {
        var json = SysEncoding.UTF8.GetBytes("[{\"n\":\"5700\",\"vb\":1}]");

        Assert.Throws<FormatException>(() => SenmlJsonCodec.Decode(json));
    }

    [Fact]
    public void Decode_TruncatedJson_ThrowsFormatException()
    {
        var json = SysEncoding.UTF8.GetBytes("[{\"n\":\"5700\",\"v\":1.0");

        Assert.Throws<FormatException>(() => SenmlJsonCodec.Decode(json));
    }

    [Fact]
    public void Decode_UnknownFieldsIgnored()
    {
        // bu, bver, s, ut and other unknown SenML fields must be skipped, not error.
        var json = SysEncoding.UTF8.GetBytes(
            "[{\"bn\":\"/3303/0/\",\"bu\":\"Cel\",\"bver\":10," +
            "\"n\":\"5700\",\"v\":23.5,\"s\":-5,\"ut\":1}]");

        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Single(decoded);
        Assert.Equal("/3303/0/5700", decoded[0].Name);
        Assert.Equal(23.5, decoded[0].Value.AsDouble());
    }

    [Fact]
    public void Decode_UnknownFieldsWithNestedObject_Skipped()
    {
        // Make sure reader.Skip() handles nested structures, not just primitives.
        var json = SysEncoding.UTF8.GetBytes(
            "[{\"n\":\"5700\",\"weird\":{\"a\":[1,2,3]},\"v\":1.0}]");

        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Single(decoded);
        Assert.Equal(1.0, decoded[0].Value.AsDouble());
    }

    [Fact]
    public void Decode_IntegerLiteral_DecodedAsInteger()
    {
        // Long-precision fidelity: encoded Integer round-trips as Integer, not Float.
        var json = SysEncoding.UTF8.GetBytes("[{\"n\":\"5851\",\"v\":75}]");

        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Equal(PayloadType.Integer, decoded[0].Value.Type);
        Assert.Equal(75L, decoded[0].Value.AsLong());
    }

    [Fact]
    public void Decode_FractionalLiteral_DecodedAsFloat()
    {
        var json = SysEncoding.UTF8.GetBytes("[{\"n\":\"5700\",\"v\":23.5}]");

        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Equal(PayloadType.Float, decoded[0].Value.Type);
    }

    [Fact]
    public void Decode_LongMaxValue_PreservesPrecision()
    {
        var json = SysEncoding.UTF8.GetBytes(
            $"[{{\"n\":\"x\",\"v\":{long.MaxValue}}}]");

        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Equal(PayloadType.Integer, decoded[0].Value.Type);
        Assert.Equal(long.MaxValue, decoded[0].Value.AsLong());
    }

    [Fact]
    public void Roundtrip_Integer_PreservesType()
    {
        var original = new List<SenmlRecord> { new(5851, PayloadValue.FromInteger(75)) };

        var json = SenmlJsonCodec.Encode(3311, 0, original);
        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Equal(PayloadType.Integer, decoded[0].Value.Type);
        Assert.Equal(75L, decoded[0].Value.AsLong());
    }
}
