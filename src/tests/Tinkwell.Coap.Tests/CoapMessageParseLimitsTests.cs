using Tinkwell.Coap;

namespace Tinkwell.Coap.Tests;

/// <summary>
/// Validates the constructor of <see cref="CoapMessageParseLimits"/>: every dimension is bounded
/// by a hard ceiling on <see cref="CoapConstants"/>, the <c>Default</c> instance lives strictly
/// inside those ceilings, and out-of-range values fail fast at construction time.
/// </summary>
public class CoapMessageParseLimitsTests
{
    [Fact]
    public void Default_HasDocumentedValues()
    {
        var d = CoapMessageParseLimits.Default;
        Assert.Equal(8 * 1024, d.MaxMessageSize);
        Assert.Equal(64, d.MaxOptionCount);
        Assert.Equal(4 * 1024, d.MaxOptionValueLength);
    }

    [Fact]
    public void Default_StaysInsideHardCeilings()
    {
        var d = CoapMessageParseLimits.Default;
        Assert.InRange(d.MaxMessageSize, CoapConstants.MinHeaderSize, CoapConstants.MaxMessageSizeCeiling);
        Assert.InRange(d.MaxOptionCount, 0, CoapConstants.MaxOptionCountCeiling);
        Assert.InRange(d.MaxOptionValueLength, 0, CoapConstants.MaxOptionValueLengthCeiling);
    }

    [Fact]
    public void Constructor_AcceptsValuesAtCeiling()
    {
        var limits = new CoapMessageParseLimits(
            maxMessageSize: CoapConstants.MaxMessageSizeCeiling,
            maxOptionCount: CoapConstants.MaxOptionCountCeiling,
            maxOptionValueLength: CoapConstants.MaxOptionValueLengthCeiling);

        Assert.Equal(CoapConstants.MaxMessageSizeCeiling, limits.MaxMessageSize);
        Assert.Equal(CoapConstants.MaxOptionCountCeiling, limits.MaxOptionCount);
        Assert.Equal(CoapConstants.MaxOptionValueLengthCeiling, limits.MaxOptionValueLength);
    }

    [Fact]
    public void Constructor_AcceptsMinimumMessageSize()
    {
        var limits = new CoapMessageParseLimits(CoapConstants.MinHeaderSize, 0, 0);
        Assert.Equal(CoapConstants.MinHeaderSize, limits.MaxMessageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(CoapConstants.MinHeaderSize - 1)]
    [InlineData(-1)]
    [InlineData(CoapConstants.MaxMessageSizeCeiling + 1)]
    public void Constructor_RejectsMaxMessageSizeOutsideRange(int badSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapMessageParseLimits(badSize, 16, 1024));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(CoapConstants.MaxOptionCountCeiling + 1)]
    public void Constructor_RejectsMaxOptionCountOutsideRange(int badCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapMessageParseLimits(1024, badCount, 1024));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(CoapConstants.MaxOptionValueLengthCeiling + 1)]
    public void Constructor_RejectsMaxOptionValueLengthOutsideRange(int badLen)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapMessageParseLimits(1024, 16, badLen));
    }

    [Fact]
    public void Constructor_ZeroOptionsAndValueLength_Allowed()
    {
        // Hard-line policy: a server that wants to refuse every option is allowed to set the
        // count cap to 0. The parser will then reject the very first option header.
        var limits = new CoapMessageParseLimits(1024, 0, 0);
        Assert.Equal(0, limits.MaxOptionCount);
        Assert.Equal(0, limits.MaxOptionValueLength);
    }
}
