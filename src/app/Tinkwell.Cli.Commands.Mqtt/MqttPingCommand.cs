using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using MQTTnet;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;

namespace Tinkwell.Cli.Commands.Mqtt;

public sealed class MqttPingSettings : TwSettings
{
    [Description("MQTT broker hostname")]
    [CommandOption("--broker|-b")]
    [DefaultValue("localhost")]
    public string Broker { get; set; } = "localhost";

    [Description("MQTT broker port")]
    [CommandOption("--port")]
    [DefaultValue(1883)]
    public int Port { get; set; } = 1883;

    [Description("Client ID (auto-generated if omitted)")]
    [CommandOption("--client-id")]
    public string? ClientId { get; set; }
}

[CliCommand("mqtt", "ping", Description = "Ping an MQTT broker and report elapsed time")]
public sealed class MqttPingCommand : AsyncCommand<MqttPingSettings>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context, MqttPingSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var factory = new MqttClientFactory();
            using var client = factory.CreateMqttClient();

            var optionsBuilder = factory.CreateClientOptionsBuilder()
                .WithTcpServer(settings.Broker, settings.Port)
                .WithClientId(settings.ClientId ?? $"tw-ping-{Guid.NewGuid():N}"[..20]);

            var sw = Stopwatch.StartNew();
            var result = await output.RunWithStatusAsync(
                $"Pinging {settings.Broker}:{settings.Port}...",
                () => client.ConnectAsync(optionsBuilder.Build(), ct));
            sw.Stop();

            if (result.ResultCode != MqttClientConnectResultCode.Success)
            {
                if (output.Format == OutputFormat.Jsonl)
                {
                    var err = JsonSerializer.Serialize(new
                    {
                        ok = false,
                        elapsedMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                        resultCode = result.ResultCode.ToString(),
                    }, JsonOptions);
                    Console.WriteLine(err);
                }
                else
                {
                    output.WriteError($"Broker returned {result.ResultCode}");
                }
                await client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), ct);
                return 1;
            }

            await client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), ct);

            var elapsedMs = sw.Elapsed.TotalMilliseconds;
            if (output.Format == OutputFormat.Jsonl)
            {
                var obj = new Dictionary<string, object?>
                {
                    ["ok"] = true,
                    ["elapsedMs"] = Math.Round(elapsedMs, 2),
                };
                if (output.Verbose)
                    obj["sessionPresent"] = result.IsSessionPresent;
                Console.WriteLine(JsonSerializer.Serialize(obj, JsonOptions));
                return 0;
            }

            output.WriteMarkup(
                $"[green]Broker reachable[/] [dim]([cyan]{elapsedMs:F0}[/] ms)[/]");
            if (output.Verbose && result.IsSessionPresent)
                output.WriteMarkup("[dim]Session was present.[/]");

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