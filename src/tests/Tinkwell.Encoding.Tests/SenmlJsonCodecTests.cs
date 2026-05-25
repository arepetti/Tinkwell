using System.Text.Json;
using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class SenmlJsonCodecTests
{
    [Fact]
    public void Encode_SingleFloat_ProducesValidJson()
    {
        var records = new List<SenmlRecord>
        {
            new(5700, PayloadValue.FromFloat(23.5)),
        };

        var json = SenmlJsonCodec.Encode(3303, 0, records);
        var text = System.Text.Encoding.UTF8.GetString(json);

        var doc = JsonDocument.Parse(text);
        var array = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, array.ValueKind);
        Assert.Single(array.EnumerateArray());

        var first = array[0];
        Assert.Equal("/3303/0/", first.GetProperty("bn").GetString());
        Assert.Equal("5700", first.GetProperty("n").GetString());
        Assert.Equal(23.5, first.GetProperty("v").GetDouble());
    }

    [Fact]
    public void Encode_StringValue_UsesVsField()
    {
        var records = new List<SenmlRecord>
        {
            new(5701, PayloadValue.FromString("Cel")),
        };

        var json = SenmlJsonCodec.Encode(3303, 0, records);
        var text = System.Text.Encoding.UTF8.GetString(json);

        var doc = JsonDocument.Parse(text);
        Assert.Equal("Cel", doc.RootElement[0].GetProperty("vs").GetString());
    }

    [Fact]
    public void Encode_BooleanValue_UsesVbField()
    {
        var records = new List<SenmlRecord>
        {
            new(5850, PayloadValue.FromBoolean(true)),
        };

        var json = SenmlJsonCodec.Encode(3306, 0, records);
        var text = System.Text.Encoding.UTF8.GetString(json);

        var doc = JsonDocument.Parse(text);
        Assert.True(doc.RootElement[0].GetProperty("vb").GetBoolean());
    }

    [Fact]
    public void Encode_WithTimestamp_IncludesT()
    {
        var ts = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var records = new List<SenmlRecord>
        {
            new(5700, PayloadValue.FromFloat(22.0), ts),
        };

        var json = SenmlJsonCodec.Encode(3303, 0, records);
        var text = System.Text.Encoding.UTF8.GetString(json);

        var doc = JsonDocument.Parse(text);
        Assert.True(doc.RootElement[0].TryGetProperty("t", out var tProp));
        Assert.Equal(ts.ToUnixTimeSeconds(), tProp.GetInt64());
    }

    [Fact]
    public void Encode_MultipleRecords_BaseNameOnlyOnFirst()
    {
        var records = new List<SenmlRecord>
        {
            new(5700, PayloadValue.FromFloat(23.5)),
            new(5701, PayloadValue.FromString("Cel")),
        };

        var json = SenmlJsonCodec.Encode(3303, 0, records);
        var text = System.Text.Encoding.UTF8.GetString(json);

        var doc = JsonDocument.Parse(text);
        Assert.True(doc.RootElement[0].TryGetProperty("bn", out _));
        Assert.False(doc.RootElement[1].TryGetProperty("bn", out _));
    }

    [Fact]
    public void Decode_FloatValue_Roundtrips()
    {
        var original = new List<SenmlRecord>
        {
            new(5700, PayloadValue.FromFloat(23.5)),
        };

        var json = SenmlJsonCodec.Encode(3303, 0, original);
        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Single(decoded);
        Assert.Equal("/3303/0/5700", decoded[0].Name);
        Assert.Equal(23.5, decoded[0].Value.AsDouble());
    }

    [Fact]
    public void Decode_StringValue_Roundtrips()
    {
        var original = new List<SenmlRecord>
        {
            new(5701, PayloadValue.FromString("Cel")),
        };

        var json = SenmlJsonCodec.Encode(3303, 0, original);
        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Single(decoded);
        Assert.Equal("Cel", decoded[0].Value.AsString());
    }

    [Fact]
    public void Decode_BooleanValue_Roundtrips()
    {
        var original = new List<SenmlRecord>
        {
            new(5850, PayloadValue.FromBoolean(false)),
        };

        var json = SenmlJsonCodec.Encode(3306, 0, original);
        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Single(decoded);
        Assert.False(decoded[0].Value.AsBoolean());
    }

    [Fact]
    public void Decode_InvalidJson_ThrowsFormatException()
    {
        var bad = System.Text.Encoding.UTF8.GetBytes("{\"not\": \"an array\"}");
        Assert.Throws<FormatException>(() => { SenmlJsonCodec.Decode(bad); });
    }

    [Fact]
    public void Decode_NoValueField_ReturnsEmpty()
    {
        var json = System.Text.Encoding.UTF8.GetBytes("[{\"n\":\"5700\"}]");
        var decoded = SenmlJsonCodec.Decode(json);
        Assert.Single(decoded);
        Assert.Equal(PayloadType.None, decoded[0].Value.Type);
    }

    [Fact]
    public void Decode_OpaqueValue_FromBase64()
    {
        var data = new byte[] { 0xDE, 0xAD };
        var original = new List<SenmlRecord>
        {
            new(100, PayloadValue.FromOpaque(data)),
        };

        var json = SenmlJsonCodec.Encode(3300, 0, original);
        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Single(decoded);
        Assert.Equal(data, (byte[])decoded[0].Value.RawValue!);
    }
}
