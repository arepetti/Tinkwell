using System.Buffers.Binary;
using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class TlvRoundtripEdgeCaseTests
{
    [Fact]
    public void Roundtrip_NegativeInteger()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 1,
            PayloadValue.FromInteger(-42), PayloadType.Integer);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Integer);
        Assert.Equal(-42, value.AsLong());
    }

    [Fact]
    public void Roundtrip_ZeroInteger()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 1,
            PayloadValue.FromInteger(0), PayloadType.Integer);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Integer);
        Assert.Equal(0, value.AsLong());
    }

    [Fact]
    public void Roundtrip_MaxInt32()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 1,
            PayloadValue.FromInteger(int.MaxValue), PayloadType.Integer);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Integer);
        Assert.Equal(int.MaxValue, value.AsLong());
    }

    [Fact]
    public void Roundtrip_MinInt32()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 1,
            PayloadValue.FromInteger(int.MinValue), PayloadType.Integer);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Integer);
        Assert.Equal(int.MinValue, value.AsLong());
    }

    [Fact]
    public void Roundtrip_FloatZero()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5700,
            PayloadValue.FromFloat(0.0), PayloadType.Float);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Float);
        Assert.Equal(0.0, value.AsDouble());
    }

    [Fact]
    public void Roundtrip_FloatNegative()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5700,
            PayloadValue.FromFloat(-273.15), PayloadType.Float);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Float);
        Assert.Equal(-273.15, value.AsDouble(), 0.01);
    }

    [Fact]
    public void Roundtrip_EmptyString()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5701,
            PayloadValue.FromString(""), PayloadType.String);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.String);
        Assert.Equal("", value.AsString());
    }

    [Fact]
    public void Roundtrip_UnicodeString()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5701,
            PayloadValue.FromString("Celsius \u00B0C"), PayloadType.String);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.String);
        Assert.Equal("Celsius \u00B0C", value.AsString());
    }

    [Fact]
    public void Roundtrip_BooleanFalse()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5850,
            PayloadValue.FromBoolean(false), PayloadType.Boolean);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Boolean);
        Assert.False(value.AsBoolean());
    }

    [Fact]
    public void Roundtrip_EmptyOpaque()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 100,
            PayloadValue.FromOpaque([]), PayloadType.Opaque);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Opaque);
        Assert.Empty((byte[])value.RawValue!);
    }

    [Fact]
    public void Roundtrip_ResourceInstanceType()
    {
        var record = new TlvRecord(
            TlvRecordType.ResourceInstance, 0,
            PayloadValue.FromFloat(42.0), PayloadType.Float);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        Assert.Equal(TlvRecordType.ResourceInstance, decoded[0].Type);
    }

    [Fact]
    public void DecodeSignedInteger_4Bytes_NegativeValue()
    {
        var buf = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, -100_000);
        Assert.Equal(-100_000, TlvDecoder.DecodeSignedInteger(buf));
    }

    [Fact]
    public void DecodeSignedInteger_8Bytes()
    {
        var buf = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buf, long.MinValue);
        Assert.Equal(long.MinValue, TlvDecoder.DecodeSignedInteger(buf));
    }

    [Fact]
    public void Interpret_UnsupportedType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            TlvDecoder.Interpret([], (PayloadType)99));
    }

    [Fact]
    public void EncodeValue_NoneType_ReturnsEmpty()
    {
        var bytes = TlvEncoder.EncodeValue(PayloadValue.Empty, PayloadType.None);
        Assert.Empty(bytes);
    }

    [Fact]
    public void EncodeValue_UnsupportedType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            TlvEncoder.EncodeValue(PayloadValue.FromString("x"), (PayloadType)99));
    }

    [Fact]
    public void Decode_MultipleConcatenatedRecords()
    {
        var records = new List<TlvRecord>
        {
            new(TlvRecordType.Resource, 5700, PayloadValue.FromFloat(1.0), PayloadType.Float),
            new(TlvRecordType.Resource, 5701, PayloadValue.FromString("X"), PayloadType.String),
            new(TlvRecordType.Resource, 5602, PayloadValue.FromFloat(2.0), PayloadType.Float),
            new(TlvRecordType.Resource, 5601, PayloadValue.FromFloat(0.5), PayloadType.Float),
            new(TlvRecordType.Resource, 5850, PayloadValue.FromBoolean(true), PayloadType.Boolean),
        };

        var bytes = TlvEncoder.Encode(records);
        var decoded = TlvDecoder.Decode(bytes);

        Assert.Equal(5, decoded.Count);
        Assert.Equal(5700, decoded[0].Identifier);
        Assert.Equal(5701, decoded[1].Identifier);
        Assert.Equal(5602, decoded[2].Identifier);
        Assert.Equal(5601, decoded[3].Identifier);
        Assert.Equal(5850, decoded[4].Identifier);

        // Also verify the values made the round trip in order (regression guard for any
        // off-by-one mismatch between identifier and value boundaries).
        Assert.Equal(1.0, TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Float).AsDouble());
        Assert.Equal("X", TlvDecoder.Interpret(decoded[1].RawValue, PayloadType.String).AsString());
        Assert.Equal(2.0, TlvDecoder.Interpret(decoded[2].RawValue, PayloadType.Float).AsDouble());
        Assert.Equal(0.5, TlvDecoder.Interpret(decoded[3].RawValue, PayloadType.Float).AsDouble());
        Assert.True(TlvDecoder.Interpret(decoded[4].RawValue, PayloadType.Boolean).AsBoolean());
    }
}
