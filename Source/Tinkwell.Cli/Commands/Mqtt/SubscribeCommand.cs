using System.ComponentModel;
using System.Text;
using MQTTnet;
using MQTTnet.Protocol;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Mqtt;

[CommandFor("subscribe", parent: typeof(MqttCommand))]
[Description("Subscribe to an MQTT topic.")]
sealed class SubscribeCommand : AsyncCommand<SubscribeCommand.Settings>
{
    public sealed class Settings : MqttCommand.Settings
    {
        [CommandArgument(0, "<TOPIC FILTER>")]
        public string Topic { get; set; } = "";

        [CommandOption("--output")]
        [Description("Saves all the received messages in the specified replay file.")]
        public string? Output { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        string? username = settings.Username;
        string? password = Environment.GetEnvironmentVariable("TINKWELL_MQTT_PASSWORD");

        if (!string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
            password = AnsiConsole.Ask<string>($"Password for '{username}'");

        using var client = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync("Configuring...", async ctx =>
            {
                var factory = new MqttClientFactory();
                var mqttClient = factory.CreateMqttClient();

                var options = new MqttClientOptionsBuilder()
                    .WithClientId(settings.ClientId)
                    .WithTcpServer(settings.BrokerAddress, settings.BrokerPort)
                    .WithCleanSession();

                if (!string.IsNullOrWhiteSpace(username))
                    options.WithCredentials(username, password);

                ctx.Status("Connecting...");
                var result = await mqttClient.ConnectAsync(options.Build());

                return mqttClient;
            });

        AnsiConsole.MarkupLineInterpolated($"Listening for [cyan]{settings.Topic}[/]...");
        var table = new PropertyValuesTable();
        await AnsiConsole.Live(table).StartAsync(async ctx =>
        {
            client.ApplicationMessageReceivedAsync += arg =>
            {
                var topic = arg.ApplicationMessage.Topic;
                var payload = arg.ApplicationMessage.ConvertPayloadToString();

                if (!string.IsNullOrWhiteSpace(settings.Output))
                {
                    string encodedTopic = Convert.ToBase64String(Encoding.UTF8.GetBytes(topic));
                    string encodedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
                    File.AppendAllText(settings.Output, $"{encodedTopic},{encodedPayload}\n");
                }

                table
                    .AddEntry("Topic", topic)
                    .AddEntry("Payload", payload)
                    .AddRow();

                ctx.Refresh();

                return Task.CompletedTask;
            };

            var topicFilter = new MqttTopicFilterBuilder()
                .WithTopic(settings.Topic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await client.SubscribeAsync(topicFilter);

            while (true)
                Console.ReadKey(true);
        });

        await client.DisconnectAsync();
        return ExitCode.Ok;
    }
}
