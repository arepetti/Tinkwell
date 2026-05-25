using Tinkwell.Encoding;
using SysEncoding = System.Text.Encoding;

namespace Tinkwell.Encoding.Tests;

public class SenmlJsonEdgeCaseTests
{
    [Fact]
    public void Roundtrip_IntegerValue()
    {
        var records = new List<SenmlRecord> { new(5851, PayloadValue.FromInteger(75)) };
        var json = SenmlJsonCodec.Encode(3311, 0, records);
        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Single(decoded);
        Assert.Equal(PayloadType.Integer, decoded[0].Value.Type);
        Assert.Equal(75L, decoded[0].Value.AsLong());
    }

    [Fact]
    public void Encode_TimeValue_WrittenAsNumericV()
    {
        // SenML JSON has no first-class time field for resource values; the encoder writes Time
        // resources as Unix seconds in `v`. The decoder consequently surfaces them as Integer
        // (callers must reinterpret with DateTimeOffset.FromUnixTimeSeconds if needed).
        var ts = new DateTimeOffset(2025, 1, 15, 8, 30, 0, TimeSpan.Zero);
        var records = new List<SenmlRecord>
        {
            new(5518, PayloadValue.FromTime(ts)),
        };

        var json = SenmlJsonCodec.Encode(3303, 0, records);
        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Equal(PayloadType.Integer, decoded[0].Value.Type);
        Assert.Equal(ts.ToUnixTimeSeconds(), decoded[0].Value.AsLong());
    }

    [Fact]
    public void Decode_MultipleRecords_AllDecoded()
    {
        var records = new List<SenmlRecord>
        {
            new(5700, PayloadValue.FromFloat(23.5)),
            new(5701, PayloadValue.FromString("Cel")),
            new(5602, PayloadValue.FromFloat(30.0)),
        };

        var json = SenmlJsonCodec.Encode(3303, 0, records);
        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Equal(3, decoded.Count);
        Assert.Equal("/3303/0/5700", decoded[0].Name);
        Assert.Equal("/3303/0/5701", decoded[1].Name);
        Assert.Equal("/3303/0/5602", decoded[2].Name);
    }

    [Fact]
    public void Decode_WithTimestamp_ParsesCorrectly()
    {
        var ts = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var records = new List<SenmlRecord>
        {
            new(5700, PayloadValue.FromFloat(22.0), ts),
        };

        var json = SenmlJsonCodec.Encode(3303, 0, records);
        var decoded = SenmlJsonCodec.Decode(json);

        Assert.NotNull(decoded[0].Timestamp);
        Assert.Equal(ts.ToUnixTimeSeconds(),
            decoded[0].Timestamp!.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public void Decode_EmptyArray_ReturnsEmpty()
    {
        var json = "[]"u8.ToArray();
        var decoded = SenmlJsonCodec.Decode(json);
        Assert.Empty(decoded);
    }

    [Fact]
    public void Encode_EmptyRecordList_ProducesEmptyArray()
    {
        var json = SenmlJsonCodec.Encode(3303, 0, Array.Empty<SenmlRecord>());
        var text = SysEncoding.UTF8.GetString(json);
        Assert.Equal("[]", text);
    }

    [Fact]
    public void Decode_BooleanFalse_Roundtrips()
    {
        var json = SysEncoding.UTF8.GetBytes("[{\"n\":\"5850\",\"vb\":false}]");
        var decoded = SenmlJsonCodec.Decode(json);
        Assert.Single(decoded);
        Assert.Equal(PayloadType.Boolean, decoded[0].Value.Type);
        Assert.False(decoded[0].Value.AsBoolean());
    }

    [Fact]
    public void Decode_NegativeFloat()
    {
        var json = SysEncoding.UTF8.GetBytes("[{\"n\":\"5700\",\"v\":-12.5}]");
        var decoded = SenmlJsonCodec.Decode(json);
        Assert.Equal(-12.5, decoded[0].Value.AsDouble());
    }

    [Fact]
    public void Decode_ZeroValue()
    {
        var json = SysEncoding.UTF8.GetBytes("[{\"n\":\"5700\",\"v\":0}]");
        var decoded = SenmlJsonCodec.Decode(json);
        Assert.Equal(0.0, decoded[0].Value.AsDouble());
    }
}
