using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Bootstrapper.Tests.Ipc;

public class NamedPipes_ClientServer
{
    [Fact]
    public void NamedPipeFactory_CreatesNewServerInstance()
    {
        var factory = new NamedPipeServerFactory();
        var instance1 = factory.Create();
        var instance2 = factory.Create();

        Assert.NotNull(instance1);
        Assert.NotNull(instance2);
        Assert.NotSame(instance1, instance2);
    }

    [Fact]
    public async Task NamedPipe_ClientServer_Communication()
    {
        var message = "Hello, pipe!";

        string pipeName = Guid.NewGuid().ToString();
        var server = new NamedPipeServer();
        using var client = new NamedPipeClient();

        server.ProcessAsync = async args =>
        {
            var received = await args.Reader.ReadLineAsync();
            args.Writer.WriteLine(received);
        };

        server.Open(pipeName);

        await client.ConnectAsync(".", pipeName, TimeSpan.FromSeconds(5));
        var reply = await client.SendCommandAndWaitReplyAsync(message);

        Assert.Equal(message, reply);
        server.Close();
    }
}
