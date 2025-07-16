using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Bootstrapper.Tests.Ipc;

public class NamedPipes_ClientServer
{
    [Fact]
    public async Task NamedPipe_ClientServer_Communication()
    {
        var pipeName = Guid.NewGuid().ToString();
        var server = new NamedPipeServer();
        var client = new NamedPipeClient();
        var message = "Hello, pipe!";
        var receivedMessage = new TaskCompletionSource<string>();

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
