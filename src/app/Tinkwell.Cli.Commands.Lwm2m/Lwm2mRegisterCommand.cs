using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Coap;
using Tinkwell.Lwm2m;

namespace Tinkwell.Cli.Commands.Lwm2m;

public sealed class RegisterSettings : Lwm2mSettings
{
    [Description("Client endpoint name")]
    [CommandArgument(0, "<endpoint>")]
    public string Endpoint { get; set; } = "";

    [Description("Object paths to register (comma-separated, e.g. 3/0,3303/0,3304/0)")]
    [CommandArgument(1, "<objects>")]
    public string Objects { get; set; } = "";

    [Description("Registration lifetime in seconds")]
    [CommandOption("--lifetime|-l")]
    [DefaultValue(300)]
    public int Lifetime { get; set; } = 300;
}

[CliCommand("lwm2m", "register", Description = "Register a device with the LwM2M server")]
public sealed class Lwm2mRegisterCommand : AsyncCommand<RegisterSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, RegisterSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var objectPaths = settings.Objects
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var payload = LinkFormatBuilder.BuildRegistrationPayload(objectPaths);
            var query = $"ep={settings.Endpoint}&lt={settings.Lifetime}";

            var request = new CoapClientRequest(
                System.Text.Encoding.UTF8.GetBytes(payload),
                CoapContentFormat.ApplicationLinkFormat)
            {
                Method = CoapMethod.Post,
            };
            var options = new CoapClientRequestOptions
            {
                Timeout = TimeSpan.FromSeconds(settings.Timeout),
            };

            var response = await output.RunWithStatusAsync(
                $"Registering '{settings.Endpoint}' at {settings.Host}:{settings.Port}...",
                () => CoapClient.SendAsync(
                    settings.Host, settings.Port, "/rd", query, request, options, ct));

            var codeStr = CoapCode.ToDisplayString(response.Code);
            bool isSuccess = (response.Code >> 5) == 2;

            if (isSuccess)
            {
                var location = response.LocationPath;
                output.WriteSuccess(
                    $"Registered [bold]{settings.Endpoint}[/] ({codeStr})" +
                    (location is not null ? $" at [cyan]{location}[/]" : ""));
            }
            else
            {
                output.WriteError($"Registration failed: {codeStr}");
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