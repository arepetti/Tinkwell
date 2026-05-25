using System.ComponentModel;
using Spectre.Console.Cli;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Cli.Commands.Measures;

internal sealed class MeasuresListSettings : MeasuresSettings
{
    [Description("Filter by category")]
    [CommandOption("--category|-c")]
    public string? Category { get; set; }
}

[Description("List all measures")]
internal sealed class MeasuresListCommand : AsyncCommand<MeasuresListSettings>
{
    private static readonly IReadOnlyList<ColumnDef<MeasureRow>> Columns =
    [
        new("Name",       r => r.Name),
        new("Value",      r => r.Value),
        new("Unit",       r => r.Unit),
        new("Category",   r => r.Category),
        new("Type",       r => r.Type,       VerboseOnly: true),
        new("Min",        r => r.Min,        VerboseOnly: true),
        new("Max",        r => r.Max,        VerboseOnly: true),
        new("TTL",        r => r.Ttl,        VerboseOnly: true),
        new("Precision",  r => r.Precision,  VerboseOnly: true),
        // The "kind" of the measure (constant / derived / system flags from
        // MeasureAttributes). Surfaced here so JSONL consumers like the
        // Studio UI can render a per-row indicator without an extra round-trip.
        new("Attributes", r => r.Attributes, VerboseOnly: true),
        new("Tags",       r => r.Tags,       VerboseOnly: true),
    ];

    public override async Task<int> ExecuteAsync(
        CommandContext context, MeasuresListSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            using var handle = await output.RunWithStatusAsync(
                "Connecting to measures service...",
                () => MeasuresHelper.ConnectAsync(settings, ct));

            var response = await output.RunWithStatusAsync(
                "Loading measures...",
                () => handle.Client.ListAsync(new MeasuresGrpc.ListMeasuresRequest(), cancellationToken: ct).ResponseAsync);

            var rows = new List<MeasureRow>();

            foreach (var m in response.Measures)
            {
                if (settings.Category is not null
                    && !string.Equals(m.Metadata?.Category, settings.Category, StringComparison.OrdinalIgnoreCase))
                    continue;

                rows.Add(ToRow(m));
            }

            output.WriteTable("Measures", Columns, rows);
            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    private static MeasureRow ToRow(MeasuresGrpc.MeasureProto m)
    {
        var def = m.Definition;
        var meta = m.Metadata;
        var val = m.Value;

        return new MeasureRow(
            def?.Name ?? "-",
            FormatValue(val),
            def?.Unit is { Length: > 0 } u ? u : "-",
            meta?.Category is { Length: > 0 } c ? c : "-",
            def?.Type ?? "-",
            def?.HasMinimum == true ? def.Minimum.ToString() : "-",
            def?.HasMaximum == true ? def.Maximum.ToString() : "-",
            def?.HasTtlSeconds == true ? def.TtlSeconds.ToString() : "-",
            def?.HasPrecision == true ? def.Precision.ToString() : "-",
            def?.Attributes is { Length: > 0 } a ? a : "-",
            meta?.Tags.Count > 0 ? string.Join(", ", meta.Tags) : "-"
        );
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

internal sealed record MeasureRow(
    string Name, string Value, string Unit, string Category,
    string Type, string Min, string Max, string Ttl, string Precision,
    string Attributes, string Tags);
