using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Coordinator;

internal sealed class RawSettings : TwCoordinatorSettings
{
    [Description("The raw pipe command string to send")]
    [CommandArgument(0, "<command>")]
    public string Command { get; set; } = "";

    [Description("Skip the confirmation prompt")]
    [CommandOption("--no-confirm|-y")]
    [DefaultValue(false)]
    public bool NoConfirm { get; set; }
}

[Description("Send a raw command string to the coordinator pipe")]
internal sealed class RawCommand : AsyncCommand<RawSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, RawSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        if (!settings.NoConfirm && !settings.NonInteractive)
        {
            var confirmed = AnsiConsole.Confirm(
                $"Send [yellow]{Markup.Escape(settings.Command)}[/] to the coordinator?",
                defaultValue: false);

            if (!confirmed)
            {
                output.WriteWarning("Cancelled");
                return 1;
            }
        }

        try
        {
            var result = await output.RunWithStatusAsync(
                "Sending command...",
                () => PipeCommandRunner.SendAsync(settings, settings.Command, ct));

            var json = JsonSerializer.Serialize(new
            {
                status = result.Status,
                message = result.Message,
                data = result.Data
            }, JsonPrintOptions);

            if (settings.NonInteractive)
            {
                Console.WriteLine(json);
            }
            else
            {
                var pretty = JsonSerializer.Serialize(new
                {
                    status = result.Status,
                    message = result.Message,
                    data = result.Data
                }, JsonPrettyOptions);

                output.WriteRawJsonColored(pretty);
            }

            return result.IsOk ? 0 : 1;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or IOException)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    private static readonly JsonSerializerOptions JsonPrintOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions JsonPrettyOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
