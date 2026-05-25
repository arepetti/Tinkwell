using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using SignalsGrpc = Tinkwell.Runlet.Signals.Grpc.V1;

namespace Tinkwell.Cli.Commands.Signals;

internal sealed class SignalsCreateSettings : SignalsSettings
{
    [Description("Signal name")]
    [CommandArgument(0, "<name>")]
    public string Name { get; set; } = "";

    [Description("When expression (trigger condition)")]
    [CommandOption("--when|-w")]
    public string? WhenExpression { get; set; }

    [Description("Until expression (hysteresis / deactivation condition)")]
    [CommandOption("--until|-u")]
    public string? UntilExpression { get; set; }

    [Description("Duration the condition must hold before firing (seconds, string, or expression)")]
    [CommandOption("--for")]
    public string? ForDuration { get; set; }

    [Description("Key=value settings to attach to the signal (repeatable)")]
    [CommandOption("--set|-s")]
    public string[]? Settings { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(WhenExpression))
            return ValidationResult.Error("--when is required");

        return base.Validate();
    }
}

[Description("Create a new signal definition")]
internal sealed class SignalsCreateCommand : AsyncCommand<SignalsCreateSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, SignalsCreateSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var request = new SignalsGrpc.CreateSignalRequest
            {
                Name = settings.Name,
                WhenExpression = settings.WhenExpression!,
            };

            if (settings.UntilExpression is not null)
                request.UntilExpression = settings.UntilExpression;

            if (settings.ForDuration is not null)
                request.ForDuration = settings.ForDuration;

            if (settings.Settings is not null)
            {
                foreach (var kv in settings.Settings)
                {
                    var eqIdx = kv.IndexOf('=');
                    if (eqIdx <= 0)
                    {
                        output.WriteError($"Invalid setting '{kv}'. Expected key=value.");
                        return 1;
                    }

                    var key = kv[..eqIdx];
                    var value = kv[(eqIdx + 1)..];
                    request.Properties[key] = value;
                }
            }

            using var handle = await output.RunWithStatusAsync(
                "Connecting to signals service...",
                () => SignalsHelper.ConnectAsync(settings, ct));

            await output.RunWithStatusAsync(
                $"Creating signal [cyan]{Markup.Escape(settings.Name)}[/]...",
                () => handle.Client.CreateAsync(request, cancellationToken: ct).ResponseAsync);

            output.WriteSuccess(
                $"Created signal [cyan]{Markup.Escape(settings.Name)}[/] " +
                $"when [dim]({Markup.Escape(settings.WhenExpression!)})[/]");
            return 0;
        }
        catch (Grpc.Core.RpcException ex)
        {
            output.WriteError(ex.Status.Detail);
            return 1;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }
}
