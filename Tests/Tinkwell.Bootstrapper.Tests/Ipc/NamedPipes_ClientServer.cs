using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Bootstrapper.Tests.Ipc;

public class NamedPipes_ClientServer : IAsyncLifetime
{
    private NamedPipeServer _server = default!;
    private NamedPipeClient _client = default!;
    private string _pipeName = default!;

    public Task InitializeAsync()
    {
        _pipeName = Guid.NewGuid().ToString();
        _server = new NamedPipeServer();
        _client = new NamedPipeClient();

        _server.ProcessAsync = async args =>
        {
            var received = await args.Reader.ReadLineAsync();
            args.Writer.WriteLine(received);
        };

        _server.Open(_pipeName);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Disconnect();
        _server.Close();
        await Task.CompletedTask;
    }
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

        await _client.ConnectAsync(".", _pipeName, TimeSpan.FromSeconds(5));
        var reply = await _client.SendCommandAndWaitReplyAsync(message);

        Assert.Equal(message, reply);
    }
}
