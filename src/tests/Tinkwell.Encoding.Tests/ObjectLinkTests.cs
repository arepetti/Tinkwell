using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class ObjectLinkTests
{
    [Fact]
    public void Constructor_StoresIds()
    {
        var link = new ObjectLink(3303, 0);
        Assert.Equal(3303, link.ObjectId);
        Assert.Equal(0, link.InstanceId);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(65536, 0)]
    [InlineData(0, 65536)]
    [InlineData(int.MaxValue, 0)]
    public void Constructor_OutOfRange_Throws(int objectId, int instanceId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectLink(objectId, instanceId));
    }

    [Fact]
    public void Null_IsMaxValuePair()
    {
        Assert.Equal(ushort.MaxValue, ObjectLink.Null.ObjectId);
        Assert.Equal(ushort.MaxValue, ObjectLink.Null.InstanceId);
        Assert.True(ObjectLink.Null.IsNull);
    }

    [Fact]
    public void IsNull_OnlyTrueForNullSentinel()
    {
        Assert.False(new ObjectLink(0, 0).IsNull);
        Assert.False(new ObjectLink(65535, 0).IsNull);
        Assert.False(new ObjectLink(0, 65535).IsNull);
        Assert.True(new ObjectLink(65535, 65535).IsNull);
    }

    [Fact]
    public void ToString_IsCanonicalForm()
    {
        Assert.Equal("3303:0", new ObjectLink(3303, 0).ToString());
        Assert.Equal("0:0", new ObjectLink(0, 0).ToString());
        Assert.Equal("65535:65535", ObjectLink.Null.ToString());
    }

    [Fact]
    public void Parse_ValidString_RoundTrips()
    {
        var link = ObjectLink.Parse("3303:0");
        Assert.Equal(3303, link.ObjectId);
        Assert.Equal(0, link.InstanceId);
    }

    [Fact]
    public void Parse_TolerantOfSurroundingWhitespace()
    {
        Assert.Equal(new ObjectLink(3303, 0), ObjectLink.Parse("  3303:0\t"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("3303")]
    [InlineData(":0")]
    [InlineData("3303:")]
    [InlineData("3303:0:1")]
    [InlineData("a:b")]
    [InlineData("3303:abc")]
    [InlineData("65536:0")]
    [InlineData("0:65536")]
    [InlineData("-1:0")]
    [InlineData("3303 :0")]
    public void Parse_InvalidString_Throws(string input)
    {
        Assert.Throws<FormatException>(() => ObjectLink.Parse(input));
    }

    [Fact]
    public void TryParse_NullInput_ReturnsFalse()
    {
        Assert.False(ObjectLink.TryParse(null, out _));
    }

    [Fact]
    public void TryParse_ValidString_ReturnsParsed()
    {
        Assert.True(ObjectLink.TryParse("1:2", out var link));
        Assert.Equal(new ObjectLink(1, 2), link);
    }

    [Fact]
    public void Equality_SameIds_AreEqual()
    {
        Assert.Equal(new ObjectLink(3303, 0), new ObjectLink(3303, 0));
        Assert.True(new ObjectLink(3303, 0) == new ObjectLink(3303, 0));
    }

    [Fact]
    public void Equality_DifferentIds_NotEqual()
    {
        Assert.NotEqual(new ObjectLink(3303, 0), new ObjectLink(3303, 1));
        Assert.NotEqual(new ObjectLink(3303, 0), new ObjectLink(3304, 0));
    }

    [Fact]
    public void Default_StructIsZeroPair()
    {
        ObjectLink def = default;
        Assert.Equal(0, def.ObjectId);
        Assert.Equal(0, def.InstanceId);
        Assert.False(def.IsNull);
    }
}
