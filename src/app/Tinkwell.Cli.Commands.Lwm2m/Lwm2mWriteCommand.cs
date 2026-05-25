using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Coap;

namespace Tinkwell.Cli.Commands.Lwm2m;

public sealed class WriteSettings : Lwm2mSettings
{
    [Description("Resource path (e.g. /3303/0/5700)")]
    [CommandArgument(0, "<path>")]
    public string ResourcePath { get; set; } = "/";

    [Description("Value to write")]
    [CommandArgument(1, "<payload>")]
    public string Payload { get; set; } = "";
}

[CliCommand("lwm2m", "write", Description = "Write a value to an LwM2M resource")]
public sealed class Lwm2mWriteCommand : AsyncCommand<WriteSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, WriteSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var request = new CoapClientRequest(
                System.Text.Encoding.UTF8.GetBytes(settings.Payload),
                CoapContentFormat.TextPlain)
            {
                Method = CoapMethod.Put,
            };
            var options = new CoapClientRequestOptions
            {
                Timeout = TimeSpan.FromSeconds(settings.Timeout),
            };

            var response = await output.RunWithStatusAsync(
                $"Writing to {settings.ResourcePath}...",
                () => CoapClient.SendAsync(
                    settings.Host, settings.Port, settings.ResourcePath,
                    query: null, request, options, ct));

            var codeStr = CoapCode.ToDisplayString(response.Code);
            bool isSuccess = (response.Code >> 5) == 2;

            if (isSuccess)
                output.WriteSuccess(
                    $"Wrote [bold]{Markup.Escape(settings.Payload)}[/] to " +
                    $"[cyan]{Markup.Escape(settings.ResourcePath)}[/] ({codeStr})");
            else
            {
                output.WriteError($"Write failed: {codeStr}");
                return 1;
            }
            return 0;
        }
        catch (OperationCanceledException)
        {
            output.WriteError("Request timed out");
            return 1;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }
}