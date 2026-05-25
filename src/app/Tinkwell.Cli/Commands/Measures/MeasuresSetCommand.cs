using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Cli.Commands.Measures;

internal sealed class MeasuresSetSettings : MeasuresSettings
{
    [Description("Measure name")]
    [CommandArgument(0, "<name>")]
    public string Name { get; set; } = "";

    [Description("Value (number or string)")]
    [CommandArgument(1, "<value>")]
    public string Value { get; set; } = "";
}

[Description("Update a measure value")]
internal sealed class MeasuresSetCommand : AsyncCommand<MeasuresSetSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, MeasuresSetSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            using var handle = await output.RunWithStatusAsync(
                "Connecting to measures service...",
                () => MeasuresHelper.ConnectAsync(settings, ct));

            var value = new MeasuresGrpc.MeasureValueProto();

            if (double.TryParse(settings.Value, System.Globalization.CultureInfo.InvariantCulture, out var d))
            {
                value.Type = "Number";
                value.NumericValue = d;
            }
            else
            {
                value.Type = "String";
                value.StringValue = settings.Value;
            }

            await output.RunWithStatusAsync(
                $"Setting [cyan]{Markup.Escape(settings.Name)}[/]...",
                () => handle.Client.UpdateAsync(
                    new MeasuresGrpc.UpdateMeasureRequest { Name = settings.Name, Value = value },
                    cancellationToken: ct).ResponseAsync);

            output.WriteSuccess(
                $"Set [cyan]{Markup.Escape(settings.Name)}[/] = {Markup.Escape(settings.Value)}");
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
