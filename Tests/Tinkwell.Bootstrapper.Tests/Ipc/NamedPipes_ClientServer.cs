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
        var pipeName = Guid.NewGuid().ToString();
        var server = new NamedPipeServer();
        var client = new NamedPipeClient();
        var message = "Hello, pipe!";

        server.Process += (sender, args) =>
        {
            var received = args.Reader.ReadLine();
            args.Writer.WriteLine(received);
        };

        server.Open(pipeName);

        await client.ConnectAsync(".", pipeName, TimeSpan.FromSeconds(5));
        var reply = await client.SendCommandAndWaitReplyAsync(message);

        Assert.Equal(message, reply);

        client.Disconnect();
        server.Close();
    }
}
