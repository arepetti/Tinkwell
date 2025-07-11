using System.ComponentModel;
using System.Text;
using MQTTnet;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Mqtt;

[CommandFor("replay", parent: typeof(MqttCommand))]
[Description("Replay a set of messages.")]
sealed class ReplayCommand : AsyncCommand<ReplayCommand.Settings>
{
    public sealed class Settings : MqttCommand.Settings
    {
        [CommandArgument(0, "<PATH>")]
        [Description("Path to the file containing the messages to replay.")]
        public string Path { get; set; } = "";

        [CommandOption("--delay")]
        [Description("Delay between messages in milliseconds.")]
        public int Delay { get; set; } = 1000;
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

                string[] messages = await File.ReadAllLinesAsync(settings.Path);
                for (int i=0; i < messages.Length; ++i)
                {
                    ctx.Status($"Publishing message {i + 1} of {messages.Length}...");
                    var parts = messages[i]
                        .Split(',', 2)
                        .Select(x => Encoding.UTF8.GetString(Convert.FromBase64String(x)))
                        .ToArray();

                    var message = new MqttApplicationMessageBuilder()
                        .WithTopic(parts[0])
                        .WithPayload(parts[1])
                        .Build();

                    await mqttClient.PublishAsync(message);
                    ctx.Status($"Published message {i + 1} of {messages.Length}...");
                    await Task.Delay(settings.Delay);
                }

                ctx.Status("Disconnecting...");
                await mqttClient.DisconnectAsync();
            });

        return ExitCode.Ok;
    }
}
