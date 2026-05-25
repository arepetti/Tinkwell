using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Cli.Commands.Measures;

internal sealed class MeasuresRegisterSettings : MeasuresSettings
{
    [Description("Measure name")]
    [CommandArgument(0, "<name>")]
    public string Name { get; set; } = "";

    [Description("Measure type: Number or String")]
    [CommandOption("--type|-t")]
    [DefaultValue("Number")]
    public string Type { get; set; } = "Number";

    [Description("UnitsNet quantity type (e.g. Temperature, Length). Default: Scalar")]
    [CommandOption("--quantity")]
    [DefaultValue("Scalar")]
    public string QuantityType { get; set; } = "Scalar";

    [Description("Unit name (e.g. DegreeCelsius)")]
    [CommandOption("--unit|-u")]
    public string? Unit { get; set; }

    [Description("Minimum value")]
    [CommandOption("--min")]
    public double? Minimum { get; set; }

    [Description("Maximum value")]
    [CommandOption("--max")]
    public double? Maximum { get; set; }

    [Description("Decimal precision")]
    [CommandOption("--precision")]
    public int? Precision { get; set; }

    [Description("TTL in seconds")]
    [CommandOption("--ttl")]
    [DefaultValue(0)]
    public int Ttl { get; set; }

    [Description("Category for grouping")]
    [CommandOption("--category|-c")]
    public string? Category { get; set; }

    [Description("Human-readable description")]
    [CommandOption("--description|-d")]
    public string? Description { get; set; }
}

[Description("Register a new measure definition")]
internal sealed class MeasuresRegisterCommand : AsyncCommand<MeasuresRegisterSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, MeasuresRegisterSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var definition = new MeasuresGrpc.MeasureDefinitionProto
            {
                Name = settings.Name,
                Type = settings.Type,
                QuantityType = settings.QuantityType,
            };

            if (settings.Unit is not null)
                definition.Unit = settings.Unit;
            if (settings.Minimum is not null)
                definition.Minimum = settings.Minimum.Value;
            if (settings.Maximum is not null)
                definition.Maximum = settings.Maximum.Value;
            if (settings.Precision is not null)
                definition.Precision = settings.Precision.Value;
            if (settings.Ttl > 0)
                definition.TtlSeconds = settings.Ttl;

            var metadata = new MeasuresGrpc.MeasureMetadataProto
            {
                Category = settings.Category ?? "",
                Description = settings.Description ?? "",
            };

            using var handle = await output.RunWithStatusAsync(
                "Connecting to measures service...",
                () => MeasuresHelper.ConnectAsync(settings, ct));

            await output.RunWithStatusAsync(
                $"Registering [cyan]{Markup.Escape(settings.Name)}[/]...",
                () => handle.Client.RegisterAsync(
                    new MeasuresGrpc.RegisterMeasureRequest { Definition = definition, Metadata = metadata },
                    cancellationToken: ct).ResponseAsync);

            output.WriteSuccess(
                $"Registered [cyan]{Markup.Escape(settings.Name)}[/] ({settings.Type}, {settings.QuantityType})");
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
