using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class TlvObjectLinkAndEdgeTests
{
    [Fact]
    public void EncodeObjectLink_ProducesFourBytesBigEndian()
    {
        var bytes = TlvEncoder.EncodeObjectLink(new ObjectLink(0x1234, 0x5678));
        Assert.Equal(new byte[] { 0x12, 0x34, 0x56, 0x78 }, bytes);
    }

    [Fact]
    public void DecodeObjectLink_ReadsFourBytesBigEndian()
    {
        var link = TlvDecoder.DecodeObjectLink([0x12, 0x34, 0x56, 0x78]);
        Assert.Equal(0x1234, link.ObjectId);
        Assert.Equal(0x5678, link.InstanceId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public void DecodeObjectLink_WrongLength_Throws(int length)
    {
        Assert.Throws<FormatException>(() => TlvDecoder.DecodeObjectLink(new byte[length]));
    }

    [Fact]
    public void Roundtrip_ObjectLink_ResourceRecord()
    {
        var original = new ObjectLink(3303, 0);
        var record = new TlvRecord(
            TlvRecordType.Resource, 5560,
            PayloadValue.FromObjectLink(original),
            PayloadType.ObjectLink);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);

        Assert.Single(decoded);
        Assert.Equal(5560, decoded[0].Identifier);
        Assert.Equal(4, decoded[0].RawValue.Length);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.ObjectLink);
        Assert.Equal(PayloadType.ObjectLink, value.Type);
        Assert.Equal(original, value.AsObjectLink());
    }

    [Fact]
    public void Roundtrip_ObjectLink_NullSentinel()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 1,
            PayloadValue.FromObjectLink(ObjectLink.Null),
            PayloadType.ObjectLink);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.ObjectLink);

        Assert.True(value.AsObjectLink().IsNull);
    }

    [Fact]
    public void EncodeFloat_NaN_RoundtripsAsNaN()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5700,
            PayloadValue.FromFloat(double.NaN), PayloadType.Float);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Float);

        Assert.True(double.IsNaN(value.AsDouble()));
    }

    [Fact]
    public void EncodeFloat_PositiveInfinity_Roundtrips()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5700,
            PayloadValue.FromFloat(double.PositiveInfinity), PayloadType.Float);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Float);

        Assert.True(double.IsPositiveInfinity(value.AsDouble()));
    }

    [Fact]
    public void EncodeFloat_NegativeInfinity_Roundtrips()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5700,
            PayloadValue.FromFloat(double.NegativeInfinity), PayloadType.Float);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Float);

        Assert.True(double.IsNegativeInfinity(value.AsDouble()));
    }

    [Fact]
    public void EncodeFloat_NegativeZero_PreservesSign()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5700,
            PayloadValue.FromFloat(-0.0), PayloadType.Float);

        var bytes = TlvEncoder.EncodeSingle(record);
        var decoded = TlvDecoder.Decode(bytes);
        var value = TlvDecoder.Interpret(decoded[0].RawValue, PayloadType.Float);

        Assert.Equal(0.0, value.AsDouble());
        Assert.True(double.IsNegative(value.AsDouble()));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(int.MaxValue)]
    public void Encode_OutOfRangeIdentifier_Throws(int badId)
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, badId,
            PayloadValue.FromInteger(1), PayloadType.Integer);

        Assert.Throws<ArgumentException>(() => TlvEncoder.EncodeSingle(record));
    }

    [Fact]
    public void Encode_NullRecordList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TlvEncoder.Encode(null!));
    }

    [Fact]
    public void EncodeSingle_NullRecord_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TlvEncoder.EncodeSingle(null!));
    }

    [Fact]
    public void Interpret_NullBuffer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TlvDecoder.Interpret(null!, PayloadType.Integer));
    }

    [Fact]
    public void Decode_ExtendedLengthExceedsBuffer_Throws()
    {
        // Resource (0xC0) | 8-bit id | length-of-length=01 (0x08): one length byte follows.
        // Type = 0xC8, id = 0x01, length byte = 0xFF, then only 2 bytes of value (need 0xFF).
        var bad = new byte[] { 0xC8, 0x01, 0xFF, 0x00, 0x01 };
        Assert.Throws<FormatException>(() => TlvDecoder.Decode(bad));
    }

    [Fact]
    public void Decode_TwoByteLengthExceedsBuffer_Throws()
    {
        // Resource (0xC0) | 8-bit id | length-of-length=10 (0x10): two length bytes follow.
        // Type = 0xD0, id = 0x01, length = 0x01 0x00 (256), then only 4 value bytes follow.
        var bad = new byte[] { 0xD0, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };
        Assert.Throws<FormatException>(() => TlvDecoder.Decode(bad));
    }

    [Fact]
    public void Encode_OneByteIdentifier_HasCorrectTypeByteHeader()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 1,
            PayloadValue.FromInteger(42), PayloadType.Integer);

        var bytes = TlvEncoder.EncodeSingle(record);

        // Resource type bits = 11 -> 0xC0; 8-bit id -> bit 5 not set; inline length = 1.
        Assert.Equal(0xC1, bytes[0]);
        Assert.Equal(1, bytes[1]); // identifier
    }

    [Fact]
    public void Encode_TwoByteIdentifier_HasIdentifierLengthBitSet()
    {
        var record = new TlvRecord(
            TlvRecordType.Resource, 5700,
            PayloadValue.FromInteger(42), PayloadType.Integer);

        var bytes = TlvEncoder.EncodeSingle(record);

        // Type bit 5 (0x20) must be set: 0xC0 | 0x20 | inline-length 1 = 0xE1.
        Assert.Equal(0xE1, bytes[0]);
        // Identifier 5700 = 0x1644 big-endian.
        Assert.Equal(0x16, bytes[1]);
        Assert.Equal(0x44, bytes[2]);
    }
}
