using Tinkwell.Runlet.I2c.Configuration;

namespace Tinkwell.Runlet.I2c.Tests;

public class ByteDecoderTests
{
    [Fact]
    public void Int8_NegativeTwosComplement()
    {
        Assert.Equal(-1.0, ByteDecoder.Decode(new byte[] { 0xFF }, I2cDataType.Int8, 1.0));
        Assert.Equal(-128.0, ByteDecoder.Decode(new byte[] { 0x80 }, I2cDataType.Int8, 1.0));
    }

    [Fact]
    public void UInt8_Identity()
    {
        Assert.Equal(255.0, ByteDecoder.Decode(new byte[] { 0xFF }, I2cDataType.UInt8, 1.0));
        Assert.Equal(0.0, ByteDecoder.Decode(new byte[] { 0x00 }, I2cDataType.UInt8, 1.0));
    }

    [Fact]
    public void Int16BE_SignedBigEndian()
    {
        Assert.Equal(-1.0, ByteDecoder.Decode(new byte[] { 0xFF, 0xFF }, I2cDataType.Int16BE, 1.0));
        Assert.Equal(256.0, ByteDecoder.Decode(new byte[] { 0x01, 0x00 }, I2cDataType.Int16BE, 1.0));
    }

    [Fact]
    public void Int16LE_SignedLittleEndian()
    {
        Assert.Equal(256.0, ByteDecoder.Decode(new byte[] { 0x00, 0x01 }, I2cDataType.Int16LE, 1.0));
    }

    [Fact]
    public void UInt16BE_FullRange()
    {
        Assert.Equal(65535.0, ByteDecoder.Decode(new byte[] { 0xFF, 0xFF }, I2cDataType.UInt16BE, 1.0));
    }

    [Fact]
    public void UInt16LE_ByteOrder()
    {
        Assert.Equal(0x1234, ByteDecoder.Decode(new byte[] { 0x34, 0x12 }, I2cDataType.UInt16LE, 1.0));
    }

    [Fact]
    public void Int32BE_FourBytes()
    {
        Assert.Equal(1.0, ByteDecoder.Decode(new byte[] { 0x00, 0x00, 0x00, 0x01 }, I2cDataType.Int32BE, 1.0));
        Assert.Equal(-1.0, ByteDecoder.Decode(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, I2cDataType.Int32BE, 1.0));
    }

    [Fact]
    public void Int32LE_FourBytes()
    {
        Assert.Equal(1.0, ByteDecoder.Decode(new byte[] { 0x01, 0x00, 0x00, 0x00 }, I2cDataType.Int32LE, 1.0));
    }

    [Fact]
    public void Float32BE_KnownIeeeVector()
    {
        // 1.0f → 0x3F800000 (big-endian bytes)
        Assert.Equal(1.0, ByteDecoder.Decode(new byte[] { 0x3F, 0x80, 0x00, 0x00 }, I2cDataType.Float32BE, 1.0), 6);
    }

    [Fact]
    public void Float32LE_KnownIeeeVector()
    {
        // 1.0f → 0x3F800000 reversed: 00 00 80 3F
        Assert.Equal(1.0, ByteDecoder.Decode(new byte[] { 0x00, 0x00, 0x80, 0x3F }, I2cDataType.Float32LE, 1.0), 6);
    }

    [Fact]
    public void Scale_IsApplied()
    {
        // IVT-like sensor: raw 1024 with scale 0.001 → 1.024
        Assert.Equal(1.024, ByteDecoder.Decode(new byte[] { 0x04, 0x00 }, I2cDataType.UInt16BE, 0.001), 6);
    }

    [Fact]
    public void UnknownDataType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ByteDecoder.Decode(new byte[] { 0 }, (I2cDataType)999, 1.0));
    }

    [Theory]
    [InlineData(I2cDataType.Int8, 1)]
    [InlineData(I2cDataType.UInt8, 1)]
    [InlineData(I2cDataType.Int16BE, 2)]
    [InlineData(I2cDataType.Int16LE, 2)]
    [InlineData(I2cDataType.UInt16BE, 2)]
    [InlineData(I2cDataType.UInt16LE, 2)]
    [InlineData(I2cDataType.Int32BE, 4)]
    [InlineData(I2cDataType.Int32LE, 4)]
    [InlineData(I2cDataType.Float32BE, 4)]
    [InlineData(I2cDataType.Float32LE, 4)]
    public void RequiredBytes_ReturnsCorrectCount(I2cDataType type, int expected)
    {
        Assert.Equal(expected, ByteDecoder.RequiredBytes(type));
    }

    [Fact]
    public void RequiredBytes_UnknownDataType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ByteDecoder.RequiredBytes((I2cDataType)999));
    }

    [Theory]
    [InlineData(I2cDataType.Int16BE)]
    [InlineData(I2cDataType.UInt16LE)]
    [InlineData(I2cDataType.Int32BE)]
    [InlineData(I2cDataType.Float32LE)]
    public void Decode_BufferTooShort_ThrowsArgumentException(I2cDataType type)
    {
        var required = ByteDecoder.RequiredBytes(type);
        var shortBuffer = new byte[required - 1];

        var ex = Assert.Throws<ArgumentException>(
            () => ByteDecoder.Decode(shortBuffer, type, 1.0));

        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decode_EmptyBuffer_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => ByteDecoder.Decode([], I2cDataType.UInt8, 1.0));
    }

    [Fact]
    public void Decode_ExactBufferSize_Succeeds()
    {
        ByteDecoder.Decode(new byte[] { 0x00, 0x01 }, I2cDataType.Int16BE, 1.0);
        ByteDecoder.Decode(new byte[] { 0x00, 0x00, 0x00, 0x01 }, I2cDataType.Int32LE, 1.0);
    }

    [Fact]
    public void Decode_OversizedBuffer_UsesLeadingBytes()
    {
        var result = ByteDecoder.Decode(new byte[] { 0x01, 0x00, 0xFF, 0xFF }, I2cDataType.UInt16BE, 1.0);
        Assert.Equal(256.0, result);
    }
}
