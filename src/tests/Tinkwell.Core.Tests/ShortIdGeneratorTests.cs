using Tinkwell;

namespace Tinkwell.Core.Tests;

public class ShortIdGeneratorTests
{
    [Fact]
    public void NewId_DefaultLength_IsEightLowercaseHex()
    {
        var id = ShortIdGenerator.NewId();
        Assert.Equal(ShortIdGenerator.IdLength, id.Length);
        Assert.Matches("^[0-9a-f]{8}$", id);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(32)]
    public void NewId_CustomLength_ProducesValidId(int len)
    {
        var id = ShortIdGenerator.NewId(len);
        Assert.Equal(len, id.Length);
        Assert.True(ShortIdGenerator.IsValid(id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    public void NewId_InvalidLength_Throws(int len)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShortIdGenerator.NewId(len));
    }

    [Theory]
    [InlineData("deadbeef", true)]
    [InlineData("DEADBEEF", false)]
    [InlineData("g1234567", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("12345678", true)]
    public void IsValid_ClassifiesInput(string? id, bool expected) =>
        Assert.Equal(expected, ShortIdGenerator.IsValid(id));
}
