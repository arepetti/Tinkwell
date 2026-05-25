using Tinkwell.Lwm2m;

namespace Tinkwell.Lwm2m.Tests;

public class Lwm2mPathEdgeCaseTests
{
    [Fact]
    public void TryParse_SingleSlash_ReturnsFalse()
    {
        Assert.False(Lwm2mPath.TryParse("/", out _));
    }

    [Fact]
    public void TryParse_TrailingSlash_StillParses()
    {
        Assert.True(Lwm2mPath.TryParse("/3303/", out var path));
        Assert.Equal(3303, path.ObjectId);
    }

    [Fact]
    public void TryParse_NegativeId_StillParses()
    {
        Assert.True(Lwm2mPath.TryParse("/-1/0/5700", out var path));
        Assert.Equal(-1, path.ObjectId);
    }

    [Fact]
    public void TryParse_LargeObjectId()
    {
        Assert.True(Lwm2mPath.TryParse("/65535", out var path));
        Assert.Equal(65535, path.ObjectId);
    }

    [Fact]
    public void TryParse_ZeroObjectId()
    {
        Assert.True(Lwm2mPath.TryParse("/0", out var path));
        Assert.Equal(0, path.ObjectId);
    }

    [Fact]
    public void TryParse_WhitespaceInput_ReturnsFalse()
    {
        Assert.False(Lwm2mPath.TryParse("  ", out _));
    }

    [Fact]
    public void TryParse_NoLeadingSlash_StillParses()
    {
        Assert.True(Lwm2mPath.TryParse("3303/0/5700", out var path));
        Assert.Equal(3303, path.ObjectId);
        Assert.Equal(0, path.InstanceId);
        Assert.Equal(5700, path.ResourceId);
    }

    [Fact]
    public void Equality_SameValues()
    {
        var a = new Lwm2mPath(3303, 0, 5700);
        var b = new Lwm2mPath(3303, 0, 5700);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentValues()
    {
        Assert.NotEqual(new Lwm2mPath(3303), new Lwm2mPath(3304));
        Assert.NotEqual(new Lwm2mPath(3303, 0), new Lwm2mPath(3303, 1));
        Assert.NotEqual(new Lwm2mPath(3303, 0, 5700), new Lwm2mPath(3303, 0, 5701));
    }

    [Fact]
    public void Equality_DifferentDepths()
    {
        Assert.NotEqual(new Lwm2mPath(3303), new Lwm2mPath(3303, 0));
        Assert.NotEqual(new Lwm2mPath(3303, 0), new Lwm2mPath(3303, 0, 5700));
    }

    [Fact]
    public void GetHashCode_SameForEqual()
    {
        var a = new Lwm2mPath(3303, 0, 5700);
        var b = new Lwm2mPath(3303, 0, 5700);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
