using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;

namespace Tinkwell.Runlet.MqttServer.Tests;

[Trait("Category", "Integration")]
public class MqttBrokerServiceTests
{
    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task Broker_StartsAndAcceptsClientRoundTrip()
    {
        var port = GetFreeLoopbackPort();
        var options = new MqttServerRunletOptions(port);
        var broker = new MqttBrokerService(options, NullLogger<MqttBrokerService>.Instance);

        await broker.StartAsync(CancellationToken.None);

        try
        {
            var factory = new MqttClientFactory();
            using var client = factory.CreateMqttClient();

            var clientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer("127.0.0.1", port)
                .WithClientId($"test-{Guid.NewGuid():N}")
                .WithCleanSession()
                .Build();

            var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.ApplicationMessageReceivedAsync += e =>
            {
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                received.TrySetResult(payload);
                return Task.CompletedTask;
            };

            var connectResult = await client.ConnectAsync(clientOptions, CancellationToken.None);
            Assert.Equal(MqttClientConnectResultCode.Success, connectResult.ResultCode);

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter("tinkwell/tests/hello")
                .Build();
            await client.SubscribeAsync(subscribeOptions, CancellationToken.None);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic("tinkwell/tests/hello")
                .WithPayload("ping")
                .Build();
            await client.PublishAsync(message, CancellationToken.None);

            var completed = await Task.WhenAny(received.Task, Task.Delay(5000));
            Assert.Same(received.Task, completed);
            Assert.Equal("ping", await received.Task);

            await client.DisconnectAsync();
        }
        finally
        {
            await broker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Broker_StopAsync_IsIdempotent()
    {
        var port = GetFreeLoopbackPort();
        var broker = new MqttBrokerService(new MqttServerRunletOptions(port),
            NullLogger<MqttBrokerService>.Instance);

        await broker.StartAsync(CancellationToken.None);
        await broker.StopAsync(CancellationToken.None);
        await broker.StopAsync(CancellationToken.None);
    }
}
