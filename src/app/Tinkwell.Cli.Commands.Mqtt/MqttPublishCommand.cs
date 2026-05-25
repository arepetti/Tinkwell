using System.ComponentModel;
using System.Text;
using MQTTnet;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;

namespace Tinkwell.Cli.Commands.Mqtt;

public sealed class MqttPublishSettings : TwSettings
{
    [Description("MQTT topic to publish to")]
    [CommandArgument(0, "<topic>")]
    public string Topic { get; set; } = "";

    [Description("Message payload")]
    [CommandArgument(1, "<payload>")]
    public string Payload { get; set; } = "";

    [Description("MQTT broker hostname")]
    [CommandOption("--broker|-b")]
    [DefaultValue("localhost")]
    public string Broker { get; set; } = "localhost";

    [Description("MQTT broker port")]
    [CommandOption("--port")]
    [DefaultValue(1883)]
    public int Port { get; set; } = 1883;

    [Description("Quality of service (0, 1, or 2)")]
    [CommandOption("--qos|-q")]
    [DefaultValue(0)]
    public int Qos { get; set; } = 0;

    [Description("Retain the message on the broker")]
    [CommandOption("--retain")]
    [DefaultValue(false)]
    public bool Retain { get; set; }

    [Description("Client ID (auto-generated if omitted)")]
    [CommandOption("--client-id")]
    public string? ClientId { get; set; }
}

[CliCommand("mqtt", "publish", Description = "Publish a message to an MQTT broker")]
public sealed class MqttPublishCommand : AsyncCommand<MqttPublishSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, MqttPublishSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var factory = new MqttClientFactory();
            using var client = factory.CreateMqttClient();

            var optionsBuilder = factory.CreateClientOptionsBuilder()
                .WithTcpServer(settings.Broker, settings.Port)
                .WithClientId(settings.ClientId ?? $"tw-cli-{Guid.NewGuid():N}"[..20]);

            await output.RunWithStatusAsync(
                $"Connecting to {settings.Broker}:{settings.Port}...",
                () => client.ConnectAsync(optionsBuilder.Build(), ct));

            var qos = settings.Qos switch
            {
                1 => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce,
                2 => MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce,
                _ => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce,
            };

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(settings.Topic)
                .WithPayload(Encoding.UTF8.GetBytes(settings.Payload))
                .WithQualityOfServiceLevel(qos)
                .WithRetainFlag(settings.Retain)
                .Build();

            await client.PublishAsync(message, ct);

            output.WriteSuccess(
                $"Published to [bold]{Markup.Escape(settings.Topic)}[/] " +
                $"({settings.Payload.Length} bytes, QoS {settings.Qos})");

            await client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), ct);
            return 0;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }
}