using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class TlvEncoderTests
{
    [Fact]
    public void EncodeSignedInteger_SmallValue_Uses1Byte()
    {
        var bytes = TlvEncoder.EncodeSignedInteger(42);
        Assert.Single(bytes);
        Assert.Equal(42, (sbyte)bytes[0]);
    }

    [Fact]
    public void EncodeSignedInteger_NegativeSmall_Uses1Byte()
    {
        var bytes = TlvEncoder.EncodeSignedInteger(-1);
        Assert.Single(bytes);
        Assert.Equal(-1, (sbyte)bytes[0]);
    }

    [Fact]
    public void EncodeSignedInteger_Int16Range_Uses2Bytes()
    {
        var bytes = TlvEncoder.EncodeSignedInteger(1000);
        Assert.Equal(2, bytes.Length);
    }

    [Fact]
    public void EncodeSignedInteger_Int32Range_Uses4Bytes()
    {
        var bytes = TlvEncoder.EncodeSignedInteger(100_000);
        Assert.Equal(4, bytes.Length);
    }

    [Fact]
    public void EncodeSignedInteger_Int64Range_Uses8Bytes()
    {
        var bytes = TlvEncoder.EncodeSignedInteger(long.MaxValue);
        Assert.Equal(8, bytes.Length);
    }

    [Fact]
    public void EncodeFloat_LosslessFloat32_Uses4Bytes()
    {
        var bytes = TlvEncoder.EncodeFloat(1.5);
        Assert.Equal(4, bytes.Length);
    }

    [Fact]
    public void EncodeFloat_HighPrecision_Uses8Bytes()
    {
        var bytes = TlvEncoder.EncodeFloat(1.0000000000001);
        Assert.Equal(8, bytes.Length);
    }

    [Fact]
    public void Encode_SingleStringResource_Roundtrips()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5701,
            PayloadValue.FromString("Cel"),
            PayloadType.String);

        var bytes = TlvEncoder.Encode([record]);
        var decoded = TlvDecoder.Decode(bytes);

        Assert.Single(decoded);
        Assert.Equal(5701, decoded[0].Identifier);
        Assert.Equal(TlvRecordType.Resource, decoded[0].Type);

        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.String);
        Assert.Equal("Cel", value.AsString());
    }

    [Fact]
    public void Encode_SingleFloatResource_Roundtrips()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5700,
            PayloadValue.FromFloat(23.5),
            PayloadType.Float);

        var bytes = TlvEncoder.Encode([record]);
        var decoded = TlvDecoder.Decode(bytes);

        Assert.Single(decoded);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Float);
        Assert.Equal(23.5, value.AsDouble());
    }

    [Fact]
    public void Encode_BooleanResource_Roundtrips()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5850,
            PayloadValue.FromBoolean(true),
            PayloadType.Boolean);

        var bytes = TlvEncoder.Encode([record]);
        var decoded = TlvDecoder.Decode(bytes);

        Assert.Single(decoded);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Boolean);
        Assert.True(value.AsBoolean());
    }

    [Fact]
    public void Encode_IntegerResource_Roundtrips()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5851,
            PayloadValue.FromInteger(75),
            PayloadType.Integer);

        var bytes = TlvEncoder.Encode([record]);
        var decoded = TlvDecoder.Decode(bytes);

        Assert.Single(decoded);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Integer);
        Assert.Equal(75, value.AsLong());
    }

    [Fact]
    public void Encode_MultipleResources_AllRoundtrip()
    {
        var records = new List<TlvRecord>
        {
            new(TlvRecordType.Resource, 5700,
                PayloadValue.FromFloat(23.5), PayloadType.Float),
            new(TlvRecordType.Resource, 5701,
                PayloadValue.FromString("Cel"), PayloadType.String),
            new(TlvRecordType.Resource, 5601,
                PayloadValue.FromFloat(18.0), PayloadType.Float),
        };

        var bytes = TlvEncoder.Encode(records);
        var decoded = TlvDecoder.Decode(bytes);

        Assert.Equal(3, decoded.Count);
        Assert.Equal(5700, decoded[0].Identifier);
        Assert.Equal(5701, decoded[1].Identifier);
        Assert.Equal(5601, decoded[2].Identifier);
    }

    [Fact]
    public void Encode_16BitIdentifier_EncodesCorrectly()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5700,
            PayloadValue.FromFloat(22.0), PayloadType.Float);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);

        Assert.Single(decoded);
        Assert.Equal(5700, decoded[0].Identifier);
    }

    [Fact]
    public void Encode_8BitIdentifier_UsesCompactForm()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 1,
            PayloadValue.FromInteger(42), PayloadType.Integer);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);

        Assert.Single(decoded);
        Assert.Equal(1, decoded[0].Identifier);
    }

    [Fact]
    public void Encode_TimeResource_Roundtrips()
    {
        var timestamp = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var record = new TlvRecord(
            TlvRecordType.Resource, 5518,
            PayloadValue.FromTime(timestamp), PayloadType.Time);

        var bytes = TlvEncoder.Encode([record]);
        var decoded = TlvDecoder.Decode(bytes);

        Assert.Single(decoded);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Time);
        Assert.Equal(timestamp, (DateTimeOffset)value.RawValue!);
    }

    [Fact]
    public void Encode_OpaqueResource_Roundtrips()
    {
        var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE };
        var record = new TlvRecord(
            TlvRecordType.Resource, 100,
            PayloadValue.FromOpaque(data), PayloadType.Opaque);

        var bytes = TlvEncoder.Encode([record]);
        var decoded = TlvDecoder.Decode(bytes);

        Assert.Single(decoded);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Opaque);
        Assert.Equal(data, (byte[])value.RawValue!);
    }

    [Fact]
    public void Encode_LargePayload_UsesMultiByteLengthField()
    {
        var largeString = new string('x', 300);
        var record = new TlvRecord(
            TlvRecordType.Resource, 5700,
            PayloadValue.FromString(largeString), PayloadType.String);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);

        Assert.Single(decoded);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.String);
        Assert.Equal(largeString, value.AsString());
    }
}
