using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class PayloadValueObjectLinkTests
{
    [Fact]
    public void FromObjectLink_IntPair_ProducesObjectLinkValue()
    {
        var v = PayloadValue.FromObjectLink(3303, 0);
        Assert.Equal(PayloadType.ObjectLink, v.Type);
        Assert.Equal(new ObjectLink(3303, 0), v.AsObjectLink());
    }

    [Fact]
    public void FromObjectLink_Struct_ProducesObjectLinkValue()
    {
        var link = new ObjectLink(1, 2);
        var v = PayloadValue.FromObjectLink(link);
        Assert.Equal(PayloadType.ObjectLink, v.Type);
        Assert.Equal(link, v.AsObjectLink());
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 65536)]
    public void FromObjectLink_OutOfRange_Throws(int objectId, int instanceId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PayloadValue.FromObjectLink(objectId, instanceId));
    }

    [Fact]
    public void AsObjectLink_FromEmpty_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PayloadValue.Empty.AsObjectLink());
    }

    [Fact]
    public void AsObjectLink_FromInteger_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PayloadValue.FromInteger(1).AsObjectLink());
    }

    [Fact]
    public void AsObjectLink_FromFloat_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PayloadValue.FromFloat(1.0).AsObjectLink());
    }

    [Fact]
    public void FromOpaque_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PayloadValue.FromOpaque(null!));
    }
}
