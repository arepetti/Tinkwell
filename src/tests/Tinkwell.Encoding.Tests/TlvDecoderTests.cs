using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class TlvDecoderTests
{
    [Fact]
    public void Decode_EmptyPayload_ReturnsEmptyList()
    {
        var result = TlvDecoder.Decode(ReadOnlySpan<byte>.Empty);
        Assert.Empty(result);
    }

    [Fact]
    public void Decode_TruncatedData_ThrowsFormatException()
    {
        var truncated = new byte[] { 0xC1 };
        Assert.Throws<FormatException>(() => TlvDecoder.Decode(truncated));
    }

    [Fact]
    public void Decode_ValueLengthExceedsBuffer_ThrowsFormatException()
    {
        // Type byte: Resource (0xC0) + 8-bit id + inline length=7 (but only 2 bytes follow)
        var bad = new byte[] { 0xC7, 0x01, 0x00, 0x00 };
        Assert.Throws<FormatException>(() => TlvDecoder.Decode(bad));
    }

    [Fact]
    public void DecodeSignedInteger_EmptyArray_ReturnsZero()
    {
        Assert.Equal(0, TlvDecoder.DecodeSignedInteger([]));
    }

    [Fact]
    public void DecodeSignedInteger_1Byte_ReturnsSignedValue()
    {
        Assert.Equal(42, TlvDecoder.DecodeSignedInteger([42]));
        Assert.Equal(-1, TlvDecoder.DecodeSignedInteger([0xFF]));
    }

    [Fact]
    public void DecodeSignedInteger_2Bytes_BigEndian()
    {
        Assert.Equal(1000, TlvDecoder.DecodeSignedInteger([0x03, 0xE8]));
    }

    [Fact]
    public void DecodeSignedInteger_InvalidLength_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() =>
            TlvDecoder.DecodeSignedInteger(new byte[5]));
    }

    [Fact]
    public void DecodeFloat_4Bytes_ReturnsFloat()
    {
        var bytes = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteSingleBigEndian(bytes, 23.5f);
        Assert.Equal(23.5, TlvDecoder.DecodeFloat(bytes), 0.001);
    }

    [Fact]
    public void DecodeFloat_8Bytes_ReturnsDouble()
    {
        var bytes = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteDoubleBigEndian(bytes, 3.14159265358979);
        Assert.Equal(3.14159265358979, TlvDecoder.DecodeFloat(bytes), 10);
    }

    [Fact]
    public void DecodeFloat_InvalidLength_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() =>
            TlvDecoder.DecodeFloat(new byte[3]));
    }

    [Fact]
    public void Interpret_Boolean_FalseForZero()
    {
        var value = TlvDecoder.Interpret([0x00], PayloadType.Boolean);
        Assert.False(value.AsBoolean());
    }

    [Fact]
    public void Interpret_Boolean_TrueForNonZero()
    {
        var value = TlvDecoder.Interpret([0x01], PayloadType.Boolean);
        Assert.True(value.AsBoolean());
    }

    [Fact]
    public void Interpret_None_ReturnsEmpty()
    {
        var value = TlvDecoder.Interpret([], PayloadType.None);
        Assert.Equal(PayloadType.None, value.Type);
    }
}
