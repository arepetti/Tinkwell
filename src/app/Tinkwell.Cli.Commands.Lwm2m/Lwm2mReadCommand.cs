using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Coap;

namespace Tinkwell.Cli.Commands.Lwm2m;

public sealed class ReadSettings : Lwm2mSettings
{
    [Description("Resource path (e.g. /3303/0/5700)")]
    [CommandArgument(0, "<path>")]
    public string ResourcePath { get; set; } = "/";

    [Description("Accept format: text, tlv, json")]
    [CommandOption("--accept|-a")]
    public string? Accept { get; set; }
}

[CliCommand("lwm2m", "read", Description = "Read a resource from an LwM2M device")]
public sealed class Lwm2mReadCommand : AsyncCommand<ReadSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, ReadSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            CoapContentFormat? accept = settings.Accept?.ToLowerInvariant() switch
            {
                "text" => CoapContentFormat.TextPlain,
                "tlv" => CoapContentFormat.ApplicationLwm2mTlv,
                "json" => CoapContentFormat.ApplicationSenmlJson,
                null => null,
                _ => CoapContentFormat.TextPlain,
            };

            var request = new CoapClientRequest([])
            {
                Method = CoapMethod.Get,
                Accept = accept,
            };
            var options = new CoapClientRequestOptions
            {
                Timeout = TimeSpan.FromSeconds(settings.Timeout),
            };

            var response = await output.RunWithStatusAsync(
                $"Reading {settings.ResourcePath}...",
                () => CoapClient.SendAsync(
                    settings.Host, settings.Port, settings.ResourcePath,
                    query: null, request, options, ct));

            var codeStr = CoapCode.ToDisplayString(response.Code);
            bool isSuccess = (response.Code >> 5) == 2;

            if (!isSuccess)
            {
                output.WriteError($"Read failed: {codeStr}");
                return 1;
            }

            output.WriteMarkup($"[green]{codeStr}[/]");

            if (response.Payload.Length == 0)
                output.WriteMarkup("[dim](no body)[/]");
            else
                output.WriteLine(response.PayloadString);

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