using Tinkwell.Runlet.ProtobufGateway;

namespace Tinkwell.Runlet.ProtobufGateway.Tests;

public sealed class PathTemplateTests
{
    [Fact]
    public void Default_RoutePattern()
    {
        var t = new PathTemplate("/{service}/{method}");
        Assert.Equal("+/+", t.RoutePattern);
    }

    [Fact]
    public void WithPrefix_RoutePattern()
    {
        var t = new PathTemplate("/rpc/{service}/{method}");
        Assert.Equal("rpc/+/+", t.RoutePattern);
    }

    [Fact]
    public void DevicePrefix_RoutePattern()
    {
        var t = new PathTemplate("/device/{service}/{method}");
        Assert.Equal("device/+/+", t.RoutePattern);
    }

    [Fact]
    public void TryExtract_DefaultPath_ExtractsCorrectly()
    {
        var t = new PathTemplate("/{service}/{method}");
        Assert.True(t.TryExtract("/tinkwell.measures.v1.Measures/Update", out var svc, out var method));
        Assert.Equal("tinkwell.measures.v1.Measures", svc);
        Assert.Equal("Update", method);
    }

    [Fact]
    public void TryExtract_WithPrefix_ExtractsCorrectly()
    {
        var t = new PathTemplate("/rpc/{service}/{method}");
        Assert.True(t.TryExtract("/rpc/tinkwell.store.v1.StateStore/Get", out var svc, out var method));
        Assert.Equal("tinkwell.store.v1.StateStore", svc);
        Assert.Equal("Get", method);
    }

    [Fact]
    public void TryExtract_WrongSegmentCount_ReturnsFalse()
    {
        var t = new PathTemplate("/{service}/{method}");
        Assert.False(t.TryExtract("/only-one-segment", out _, out _));
    }

    [Fact]
    public void TryExtract_TooManySegments_ReturnsFalse()
    {
        var t = new PathTemplate("/{service}/{method}");
        Assert.False(t.TryExtract("/a/b/c", out _, out _));
    }

    [Fact]
    public void TryExtract_EmptyService_ReturnsFalse()
    {
        var t = new PathTemplate("/{service}/{method}");
        Assert.False(t.TryExtract("//Update", out _, out _));
    }

    [Fact]
    public void MissingServicePlaceholder_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PathTemplate("/{method}"));
    }

    [Fact]
    public void MissingMethodPlaceholder_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PathTemplate("/{service}"));
    }
}
