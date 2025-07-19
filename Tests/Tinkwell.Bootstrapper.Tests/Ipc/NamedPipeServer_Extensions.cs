
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;

namespace Tinkwell.Bootstrapper.Tests.Ipc;

public class NamedPipeServer_Extensions
{
    [Fact]
    [Trait("Category", "CI-Disabled")]
    public void SendCommandToSupervisorAndDisconnect_SendsAndReceivesMessage()
    {
        var pipeName = Guid.NewGuid().ToString();
        var server = new NamedPipeServer();
        var client = new NamedPipeClient();
        var message = "test command";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Supervisor:CommandServer:PipeName", pipeName }
            })
            .Build();

        server.ProcessAsync = async args =>
        {
            var received = await args.Reader.ReadLineAsync();
            if (received == "exit") return;
            args.Writer.WriteLine(received);
        };
        server.Open(pipeName);

        var reply = client.SendCommandToSupervisorAndDisconnect(config, message);

        Assert.Equal(message, reply);
        server.Close();
    }

    [Fact]
    public async Task SendCommandToSupervisorAndDisconnectAsync_SendsAndReceivesMessage()
    {
        var pipeName = Guid.NewGuid().ToString();
        var server = new NamedPipeServer();
        using var client = new NamedPipeClient();
        var message = "test command";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Supervisor:CommandServer:PipeName", pipeName }
            })
            .Build();

        server.ProcessAsync = async args =>
        {
            var received = await args.Reader.ReadLineAsync();
            if (received == "exit") return;
            args.Writer.WriteLine(received);
        };
        server.Open(pipeName);

        var reply = await client.SendCommandToSupervisorAndDisconnectAsync(config, message);

        Assert.Equal(message, reply);
        server.Close();
    }

    [Fact]
    public async Task SendCommandToSupervisorAndDisconnectAsync_WithGenericType_SendsAndReceivesMessage()
    {
        var pipeName = Guid.NewGuid().ToString();
        var server = new NamedPipeServer();
        using var client = new NamedPipeClient();
        var message = new { A = 1, B = "test" };
        var messageJson = JsonSerializer.Serialize(message);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Supervisor:CommandServer:PipeName", pipeName }
            })
            .Build();

        server.ProcessAsync = async args =>
        {
            var received = await args.Reader.ReadLineAsync();
            if (received == "exit") return;
            args.Writer.WriteLine(received);
        };
        server.Open(pipeName);

        var reply = await client.SendCommandToSupervisorAndDisconnectAsync<JsonElement>(config, messageJson);

        Assert.Equal(message.A, reply.GetProperty("A").GetInt32());
        Assert.Equal(message.B, reply.GetProperty("B").GetString());
        server.Close();
    }
}
