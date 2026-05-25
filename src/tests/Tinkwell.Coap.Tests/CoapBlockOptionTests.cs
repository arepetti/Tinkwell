using Tinkwell.Coap;

namespace Tinkwell.Coap.Tests;

public class CoapBlockOptionTests
{
    [Fact]
    public void FromUInt_DecodesFirstBlock_MoreSet()
    {
        // NUM=0, M=1, SZX=6 → value = (0 << 4) | (1 << 3) | 6 = 0x0E
        var block = CoapBlockOption.FromUInt(0x0E);

        Assert.Equal(0, block.Number);
        Assert.True(block.More);
        Assert.Equal(6, block.SizeExponent);
    }

    [Fact]
    public void FromUInt_DecodesLastBlock_MoreClear()
    {
        // NUM=5, M=0, SZX=6 → value = (5 << 4) | (0 << 3) | 6 = 0x56
        var block = CoapBlockOption.FromUInt(0x56);

        Assert.Equal(5, block.Number);
        Assert.False(block.More);
        Assert.Equal(6, block.SizeExponent);
    }

    [Fact]
    public void ToUInt_RoundTrips()
    {
        var original = new CoapBlockOption(Number: 42, More: true, SizeExponent: 4);
        var encoded = original.ToUInt();
        var decoded = CoapBlockOption.FromUInt(encoded);

        Assert.Equal(original.Number, decoded.Number);
        Assert.Equal(original.More, decoded.More);
        Assert.Equal(original.SizeExponent, decoded.SizeExponent);
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(1, 32)]
    [InlineData(2, 64)]
    [InlineData(3, 128)]
    [InlineData(4, 256)]
    [InlineData(5, 512)]
    [InlineData(6, 1024)]
    public void BlockSize_MatchesSzxExponent(int szx, int expectedSize)
    {
        var block = new CoapBlockOption(0, false, szx);
        Assert.Equal(expectedSize, block.BlockSize);
    }

    [Theory]
    [InlineData(0, 6, 0)]
    [InlineData(1, 6, 1024)]
    [InlineData(3, 4, 768)]     // 3 * 256
    [InlineData(10, 5, 5120)]   // 10 * 512
    public void Offset_IsBlockNumberTimesBlockSize(int num, int szx, int expectedOffset)
    {
        var block = new CoapBlockOption(num, false, szx);
        Assert.Equal(expectedOffset, block.Offset);
    }

    [Fact]
    public void FromUInt_Szx7_Reserved_ThrowsFormatException()
    {
        // SZX=7 in low 3 bits → e.g. value 0x07 (NUM=0, M=0, SZX=7)
        Assert.Throws<FormatException>(() => CoapBlockOption.FromUInt(0x07));
    }

    [Fact]
    public void FromOption_Szx7_PropagatesFormatException()
    {
        var opt = new CoapOption(CoapOptionNumber.Block2, [0x07]);
        Assert.Throws<FormatException>(() => CoapBlockOption.FromOption(opt));
    }

    [Fact]
    public void FromOption_FiveByteValue_ThrowsInvalidOperationException()
    {
        var opt = new CoapOption(CoapOptionNumber.Block1, [1, 2, 3, 4, 5]);
        Assert.Throws<InvalidOperationException>(() => CoapBlockOption.FromOption(opt));
    }

    [Fact]
    public void FromOption_FourByteValueOverflowingInt_ThrowsOverflowException()
    {
        var opt = new CoapOption(CoapOptionNumber.Block2, [0xFF, 0xFF, 0xFF, 0xFF]);
        Assert.Throws<OverflowException>(() => CoapBlockOption.FromOption(opt));
    }

    [Fact]
    public void FromOption_DecodesRawOption()
    {
        // Encode value 0x1E = (1 << 4) | (1 << 3) | 6 → NUM=1, M=1, SZX=6
        var opt = new CoapOption(CoapOptionNumber.Block1, [0x1E]);
        var block = CoapBlockOption.FromOption(opt);

        Assert.Equal(1, block.Number);
        Assert.True(block.More);
        Assert.Equal(6, block.SizeExponent);
    }

    [Fact]
    public void FromUInt_LargeBlockNumber()
    {
        // NUM=1000, M=0, SZX=6 → value = (1000 << 4) | 6 = 16006
        var block = CoapBlockOption.FromUInt(16006);

        Assert.Equal(1000, block.Number);
        Assert.False(block.More);
        Assert.Equal(6, block.SizeExponent);
    }

    [Fact]
    public void ToUInt_ZeroBlock_NoMore_MinSzx()
    {
        var block = new CoapBlockOption(0, false, 0);
        Assert.Equal(0, block.ToUInt());
    }

    [Theory]
    [InlineData(0x00, 0, false, 0)]
    [InlineData(0x08, 0, true, 0)]
    [InlineData(0x16, 1, false, 6)]
    [InlineData(0x1E, 1, true, 6)]
    public void FromUInt_WireValues(int wireValue, int num, bool more, int szx)
    {
        var block = CoapBlockOption.FromUInt(wireValue);
        Assert.Equal(num, block.Number);
        Assert.Equal(more, block.More);
        Assert.Equal(szx, block.SizeExponent);
    }
}
