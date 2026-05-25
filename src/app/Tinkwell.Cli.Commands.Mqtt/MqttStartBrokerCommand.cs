using System.ComponentModel;
using System.Net;
using MQTTnet.Server;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;

namespace Tinkwell.Cli.Commands.Mqtt;

public sealed class MqttStartBrokerSettings : TwSettings
{
    [Description("TCP port to listen on")]
    [CommandOption("--port")]
    [DefaultValue(1883)]
    public int Port { get; set; } = 1883;
}

[CliCommand("mqtt", "start-broker", Description = "Start a development MQTT broker (Ctrl+C to stop)")]
public sealed class MqttStartBrokerCommand : AsyncCommand<MqttStartBrokerSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, MqttStartBrokerSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var serverOptions = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointBoundIPAddress(IPAddress.Any)
                .WithDefaultEndpointPort(settings.Port)
                .Build();

            var factory = new MqttServerFactory();
            var server = factory.CreateMqttServer(serverOptions);

            server.ClientConnectedAsync += e =>
            {
                output.WriteMarkup($"[green]+[/] Client [cyan]{Markup.Escape(e.ClientId)}[/] connected");
                return Task.CompletedTask;
            };

            server.ClientDisconnectedAsync += e =>
            {
                output.WriteMarkup($"[red]-[/] Client [cyan]{Markup.Escape(e.ClientId)}[/] disconnected");
                return Task.CompletedTask;
            };

            await server.StartAsync();
            output.WriteMarkup(
                $"MQTT broker listening on port [bold]{settings.Port}[/] [dim](Ctrl+C to stop)[/]");

            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
            }

            await server.StopAsync();
            server.Dispose();

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