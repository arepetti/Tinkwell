using System.ComponentModel;
using Spectre.Console.Cli;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Cli.Commands.Measures;

internal sealed class MeasuresGetSettings : MeasuresSettings
{
    [Description("Measure name")]
    [CommandArgument(0, "<name>")]
    public string Name { get; set; } = "";
}

[Description("Get a single measure")]
internal sealed class MeasuresGetCommand : AsyncCommand<MeasuresGetSettings>
{
    private static readonly IReadOnlyList<ColumnDef<MeasureDetailRow>> Columns =
    [
        new("Name",         r => r.Name),
        new("Type",         r => r.Type),
        new("Value",        r => r.Value),
        new("Unit",         r => r.Unit),
        new("Quantity",     r => r.QuantityType),
        new("Category",     r => r.Category),
        new("Description",  r => r.Description),
        new("Min",          r => r.Min,          VerboseOnly: true),
        new("Max",          r => r.Max,          VerboseOnly: true),
        new("TTL",          r => r.Ttl,          VerboseOnly: true),
        new("Precision",    r => r.Precision,    VerboseOnly: true),
        new("Attributes",   r => r.Attributes,   VerboseOnly: true),
        new("Tags",         r => r.Tags,         VerboseOnly: true),
    ];

    public override async Task<int> ExecuteAsync(
        CommandContext context, MeasuresGetSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            using var handle = await output.RunWithStatusAsync(
                "Connecting to measures service...",
                () => MeasuresHelper.ConnectAsync(settings, ct));

            var response = await output.RunWithStatusAsync(
                $"Getting [cyan]{settings.Name}[/]...",
                () => handle.Client.GetAsync(
                    new MeasuresGrpc.GetMeasureRequest { Name = settings.Name },
                    cancellationToken: ct).ResponseAsync);

            if (!response.Found)
            {
                output.WriteError($"Measure '{settings.Name}' not found");
                return 1;
            }

            var m = response.Measure;
            var def = m.Definition;
            var meta = m.Metadata;
            var val = m.Value;

            var row = new MeasureDetailRow(
                def?.Name ?? "-",
                def?.Type ?? "-",
                FormatValue(val),
                def?.Unit is { Length: > 0 } u ? u : "-",
                def?.QuantityType ?? "-",
                meta?.Category is { Length: > 0 } c ? c : "-",
                meta?.Description is { Length: > 0 } d ? d : "-",
                def?.HasMinimum == true ? def.Minimum.ToString() : "-",
                def?.HasMaximum == true ? def.Maximum.ToString() : "-",
                def?.HasTtlSeconds == true ? def.TtlSeconds.ToString() : "-",
                def?.HasPrecision == true ? def.Precision.ToString() : "-",
                def?.Attributes ?? "-",
                meta?.Tags.Count > 0 ? string.Join(", ", meta.Tags) : "-"
            );

            output.WriteObject($"Measure: {settings.Name}", Columns, row);
            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    private static string FormatValue(MeasuresGrpc.MeasureValueProto? val)
    {
        if (val is null || val.Type is "Undefined" or "")
            return "-";

        if (val.Type == "Number")
            return val.NumericValue.ToString("G");

        if (val.Type == "String")
            return val.StringValue;

        return "-";
    }
}

internal sealed record MeasureDetailRow(
    string Name, string Type, string Value, string Unit, string QuantityType,
    string Category, string Description,
    string Min, string Max, string Ttl, string Precision,
    string Attributes, string Tags);
