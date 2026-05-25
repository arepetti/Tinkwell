using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Coap;
using Tinkwell.Lwm2m;

namespace Tinkwell.Cli.Commands.Lwm2m;

public sealed class UpdateSettings : Lwm2mSettings
{
    [Description("Registration location path (e.g. rd/abc123)")]
    [CommandArgument(0, "<location>")]
    public string Location { get; set; } = "";

    [Description("Updated lifetime in seconds (optional)")]
    [CommandOption("--lifetime|-l")]
    public int? Lifetime { get; set; }

    [Description("Updated object paths (comma-separated, optional)")]
    [CommandOption("--objects|-o")]
    public string? Objects { get; set; }
}

[CliCommand("lwm2m", "update", Description = "Update an existing LwM2M registration")]
public sealed class Lwm2mUpdateCommand : AsyncCommand<UpdateSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, UpdateSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var path = "/" + settings.Location.TrimStart('/');

            var queryParts = new List<string>();
            if (settings.Lifetime.HasValue)
                queryParts.Add($"lt={settings.Lifetime.Value}");
            var query = queryParts.Count > 0 ? string.Join("&", queryParts) : null;

            byte[]? payload = null;
            if (settings.Objects is not null)
            {
                var objectPaths = settings.Objects
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                payload = System.Text.Encoding.UTF8.GetBytes(
                    LinkFormatBuilder.BuildRegistrationPayload(objectPaths));
            }

            var request = new CoapClientRequest(payload ?? [])
            {
                Method = CoapMethod.Post,
                ContentFormat = payload is not null
                    ? CoapContentFormat.ApplicationLinkFormat
                    : null,
            };
            var options = new CoapClientRequestOptions
            {
                Timeout = TimeSpan.FromSeconds(settings.Timeout),
            };

            var response = await output.RunWithStatusAsync(
                $"Updating registration at {path}...",
                () => CoapClient.SendAsync(
                    settings.Host, settings.Port, path, query, request, options, ct));

            var codeStr = CoapCode.ToDisplayString(response.Code);
            bool isSuccess = (response.Code >> 5) == 2;

            if (isSuccess)
                output.WriteSuccess($"Registration updated ({codeStr})");
            else
            {
                output.WriteError($"Update failed: {codeStr}");
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