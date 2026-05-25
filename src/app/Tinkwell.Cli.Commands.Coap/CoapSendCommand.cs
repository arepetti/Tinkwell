using System.Buffers.Binary;
using System.ComponentModel;
using System.Text;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Coap;

namespace Tinkwell.Cli.Commands.Coap;

public sealed class CoapSendSettings : TwSettings
{
    [Description("CoAP method: get, post, put, delete")]
    [CommandArgument(0, "<method>")]
    public string Method { get; set; } = "get";

    [Description("CoAP URI path (e.g. /sensor/temperature)")]
    [CommandArgument(1, "<path>")]
    public string UriPath { get; set; } = "/";

    [Description("Target host")]
    [CommandOption("--host|-H")]
    [DefaultValue("localhost")]
    public string Host { get; set; } = "localhost";

    [Description("Target UDP port")]
    [CommandOption("--port")]
    [DefaultValue(5683)]
    public int Port { get; set; } = 5683;

    [Description("Request payload (for POST/PUT)")]
    [CommandOption("--payload|-d")]
    public string? Payload { get; set; }

    [Description("Accept format: text, binary, json")]
    [CommandOption("--accept|-a")]
    public string? Accept { get; set; }

    [Description("Response timeout in seconds")]
    [CommandOption("--timeout|-t")]
    [DefaultValue(5)]
    public int Timeout { get; set; } = 5;
}

[CliCommand("coap", "send", Description = "Send a CoAP request to a UDP endpoint")]
public sealed class CoapSendCommand : AsyncCommand<CoapSendSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, CoapSendSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            CoapMethod method = settings.Method.ToLowerInvariant() switch
            {
                "get" => CoapMethod.Get,
                "post" => CoapMethod.Post,
                "put" => CoapMethod.Put,
                "delete" => CoapMethod.Delete,
                _ => throw new TwCommandException(
                    $"Unknown method '{settings.Method}'. Use get, post, put, or delete."),
            };

            CoapContentFormat? accept = settings.Accept?.ToLowerInvariant() switch
            {
                "text" => CoapContentFormat.TextPlain,
                "binary" or "octet-stream" => CoapContentFormat.ApplicationOctetStream,
                "json" => CoapContentFormat.ApplicationJson,
                null => null,
                _ => CoapContentFormat.TextPlain,
            };

            byte[] payload = settings.Payload is not null
                ? Encoding.UTF8.GetBytes(settings.Payload) : [];

            var request = new CoapClientRequest(payload)
            {
                Method = method,
                Accept = accept,
            };
            var options = new CoapClientRequestOptions
            {
                Timeout = TimeSpan.FromSeconds(settings.Timeout),
            };

            var response = await output.RunWithStatusAsync(
                $"Sending {settings.Method.ToUpper()} {settings.UriPath} to " +
                $"{settings.Host}:{settings.Port}...",
                () => CoapClient.SendAsync(
                    settings.Host, settings.Port, settings.UriPath,
                    query: null, request, options, ct));

            DisplayResponse(output, response, settings);
            return 0;
        }
        catch (OperationCanceledException)
        {
            output.WriteError("Request timed out");
            return 1;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    private static void DisplayResponse(
        OutputContext output, CoapMessage response, CoapSendSettings settings)
    {
        string codeStr = CoapCode.ToDisplayString(response.Code);
        int cls = response.Code >> 5;
        bool isSuccess = cls == 2;
        string statusColor = isSuccess ? "green" : "red";

        output.WriteMarkup($"[{statusColor}]{codeStr}[/]");

        if (response.Payload.Length == 0)
        {
            output.WriteMarkup("[dim](no body)[/]");
            return;
        }

        bool isBinary = settings.Accept?.ToLowerInvariant() == "binary";
        if (isBinary && response.Payload.Length == 4)
        {
            var value = BinaryPrimitives.ReadSingleBigEndian(response.Payload);
            output.WriteMarkup($"[cyan]{value}[/] [dim](float32 big-endian)[/]");
        }
        else
        {
            output.WriteLine(response.PayloadString);
        }
    }
}