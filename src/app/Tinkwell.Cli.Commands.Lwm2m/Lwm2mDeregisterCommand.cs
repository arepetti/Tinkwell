using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Coap;

namespace Tinkwell.Cli.Commands.Lwm2m;

public sealed class DeregisterSettings : Lwm2mSettings
{
    [Description("Registration location path (e.g. rd/abc123)")]
    [CommandArgument(0, "<location>")]
    public string Location { get; set; } = "";
}

[CliCommand("lwm2m", "deregister", Description = "Deregister a client from the LwM2M server")]
public sealed class Lwm2mDeregisterCommand : AsyncCommand<DeregisterSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, DeregisterSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var path = "/" + settings.Location.TrimStart('/');

            var request = new CoapClientRequest([])
            {
                Method = CoapMethod.Delete,
            };
            var options = new CoapClientRequestOptions
            {
                Timeout = TimeSpan.FromSeconds(settings.Timeout),
            };

            var response = await output.RunWithStatusAsync(
                $"Deregistering at {path}...",
                () => CoapClient.SendAsync(
                    settings.Host, settings.Port, path,
                    query: null, request, options, ct));

            var codeStr = CoapCode.ToDisplayString(response.Code);
            bool isSuccess = (response.Code >> 5) == 2;

            if (isSuccess)
                output.WriteSuccess($"Deregistered ({codeStr})");
            else
            {
                output.WriteError($"Deregistration failed: {codeStr}");
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