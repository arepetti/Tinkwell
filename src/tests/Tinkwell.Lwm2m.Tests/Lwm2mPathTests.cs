using Tinkwell.Lwm2m;

namespace Tinkwell.Lwm2m.Tests;

public class Lwm2mPathTests
{
    [Theory]
    [InlineData("/3303", 3303, null, null)]
    [InlineData("/3303/0", 3303, 0, null)]
    [InlineData("/3303/0/5700", 3303, 0, 5700)]
    [InlineData("/3/0/1", 3, 0, 1)]
    public void TryParse_ValidPaths_ParsesCorrectly(
        string input, int objectId, int? instanceId, int? resourceId)
    {
        Assert.True(Lwm2mPath.TryParse(input, out var path));
        Assert.Equal(objectId, path.ObjectId);
        Assert.Equal(instanceId, path.InstanceId);
        Assert.Equal(resourceId, path.ResourceId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/abc")]
    [InlineData("/3303/0/5700/extra")]
    public void TryParse_InvalidPaths_ReturnsFalse(string? input)
    {
        Assert.False(Lwm2mPath.TryParse(input, out _));
    }

    [Fact]
    public void IsObject_TrueForObjectOnlyPath()
    {
        var path = new Lwm2mPath(3303);
        Assert.True(path.IsObject);
        Assert.False(path.IsInstance);
        Assert.False(path.IsResource);
    }

    [Fact]
    public void IsInstance_TrueForObjectInstancePath()
    {
        var path = new Lwm2mPath(3303, 0);
        Assert.False(path.IsObject);
        Assert.True(path.IsInstance);
        Assert.False(path.IsResource);
    }

    [Fact]
    public void IsResource_TrueForFullPath()
    {
        var path = new Lwm2mPath(3303, 0, 5700);
        Assert.False(path.IsObject);
        Assert.False(path.IsInstance);
        Assert.True(path.IsResource);
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        Assert.Equal("/3303", new Lwm2mPath(3303).ToString());
        Assert.Equal("/3303/0", new Lwm2mPath(3303, 0).ToString());
        Assert.Equal("/3303/0/5700", new Lwm2mPath(3303, 0, 5700).ToString());
    }
}
