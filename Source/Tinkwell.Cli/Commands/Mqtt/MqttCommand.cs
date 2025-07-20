using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Mqtt;

[CommandFor("mqtt")]
[Description("MQTT debug helpers.")]
public sealed class MqttCommand : Command<MqttCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--address")]
        public string BrokerAddress { get; set; } = "localhost";

        [CommandOption("--port")]
        public int BrokerPort { get; set; } = 1883;

        [CommandOption("--client-id")]
        public string ClientId { get; set; } = $"TinkwellCliMqttClient-{Guid.NewGuid()}";

        [CommandOption("--username")]
        public string? Username { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}
