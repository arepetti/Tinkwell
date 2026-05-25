using Tinkwell.Lwm2m.Server;

namespace Tinkwell.Lwm2m.Server.Tests;

public class Lwm2mServerOptionsTests
{
    [Fact]
    public void DefaultPort_Is5683()
    {
        var opts = new Lwm2mServerOptions();
        Assert.Equal(5683, opts.Port);
    }

    [Fact]
    public void DefaultName_IsNull()
    {
        var opts = new Lwm2mServerOptions();
        Assert.Null(opts.Name);
    }

    [Fact]
    public void Port_CanBeSet()
    {
        var opts = new Lwm2mServerOptions { Port = 5684 };
        Assert.Equal(5684, opts.Port);
    }

    [Fact]
    public void Name_CanBeSet()
    {
        var opts = new Lwm2mServerOptions { Name = "test-server" };
        Assert.Equal("test-server", opts.Name);
    }
}
