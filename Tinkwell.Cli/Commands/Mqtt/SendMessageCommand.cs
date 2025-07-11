using System.ComponentModel;
using MQTTnet;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Mqtt;

[CommandFor("send", parent: typeof(MqttCommand))]
[Description("Send an MQTT message.")]
sealed class SendMessageCommand : AsyncCommand<SendMessageCommand.Settings>
{
    public sealed class Settings : MqttCommand.Settings
    {
        [CommandArgument(0, "<TOPIC>")]
        public string Topic { get; set; } = "";

        [CommandArgument(1, "<PAYLOAD>")]
        public string Message { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        string? username = settings.Username;
        string? password = Environment.GetEnvironmentVariable("TINKWELL_MQTT_PASSWORD");

        if (!string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
            password = AnsiConsole.Ask<string>($"Password for '{username}'");

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync("Configuring...", async ctx =>
            {
                var factory = new MqttClientFactory();
                using var mqttClient = factory.CreateMqttClient();

                var options = new MqttClientOptionsBuilder()
                    .WithClientId(settings.ClientId)
                    .WithTcpServer(settings.BrokerAddress, settings.BrokerPort)
                    .WithCleanSession();

                if (!string.IsNullOrWhiteSpace(username))
                    options.WithCredentials(username, password);

                ctx.Status("Connecting...");
                var result = await mqttClient.ConnectAsync(options.Build());

                ctx.Status("Publishing...");
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(settings.Topic)
                    .WithPayload(settings.Message)
                    .Build();

                await mqttClient.PublishAsync(message);

                ctx.Status("Disconnecting...");
                await mqttClient.DisconnectAsync();
            });

        AnsiConsole.MarkupLine($"[green]Message sent to {settings.Topic}: {settings.Message}[/]");

        return ExitCode.Ok;
    }
}
