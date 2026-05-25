using System.Globalization;
using System.Text.Json;
using Tinkwell.Measures;

namespace Tinkwell.Runlet.Measures.Registry;

/// <summary>
/// Serialization helpers for storing measure definitions and values as JSON
/// strings in the state store.
/// </summary>
internal static class MeasureJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string SerializeDefinition(MeasureDefinition def, MeasureMetadata? meta)
    {
        var dto = new DefinitionDto
        {
            Type = def.Type.ToString(),
            Attributes = def.Attributes.ToString(),
            QuantityType = def.QuantityType,
            Unit = def.Unit,
            Minimum = def.Minimum,
            Maximum = def.Maximum,
            Precision = def.Precision,
            TtlSeconds = def.Ttl?.TotalSeconds,
            Description = meta?.Description,
            Category = meta?.Category,
            Tags = meta?.Tags is { Count: > 0 } t ? t.ToList() : null,
            CreatedAt = meta?.CreatedAt.ToString("o"),
        };

        return JsonSerializer.Serialize(dto, Options);
    }

    public static (MeasureDefinition Definition, MeasureMetadata Metadata)
        DeserializeDefinition(string name, string json)
    {
        var dto = JsonSerializer.Deserialize<DefinitionDto>(json, Options)
            ?? throw new MeasureException($"Failed to deserialize definition for '{name}'.");

        var def = new MeasureDefinition
        {
            Name = name,
            Type = Enum.Parse<MeasureType>(dto.Type ?? "Number"),
            Attributes = Enum.TryParse<MeasureAttributes>(dto.Attributes, out var a)
                ? a : MeasureAttributes.None,
            QuantityType = dto.QuantityType ?? "Scalar",
            Unit = dto.Unit,
            Minimum = dto.Minimum,
            Maximum = dto.Maximum,
            Precision = dto.Precision,
        };

        if (dto.TtlSeconds is > 0)
            def.Ttl = TimeSpan.FromSeconds(dto.TtlSeconds.Value);

        var meta = new MeasureMetadata
        {
            Description = dto.Description,
            Category = dto.Category,
            Tags = dto.Tags ?? [],
            CreatedAt = dto.CreatedAt is not null
                ? DateTime.Parse(dto.CreatedAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind)
                : DateTime.UtcNow,
        };

        return (def, meta);
    }

    public static string SerializeValue(MeasureValue value)
    {
        var dto = new ValueDto
        {
            Type = value.Type.ToString(),
            Timestamp = value.Timestamp.ToString("o"),
        };

        switch (value.Type)
        {
            case MeasureValueType.Number:
                var q = value.AsQuantity();
                dto.Value = ((double)q.Value).ToString("R", CultureInfo.InvariantCulture);
                dto.Unit = q.Unit.ToString();
                dto.QuantityType = q.QuantityInfo.Name;
                break;
            case MeasureValueType.String:
                dto.Value = value.AsString();
                break;
        }

        return JsonSerializer.Serialize(dto, Options);
    }

    public static MeasureValue DeserializeValue(MeasureDefinition definition, string json)
    {
        var dto = JsonSerializer.Deserialize<ValueDto>(json, Options)
            ?? throw new MeasureException("Failed to deserialize measure value.");

        if (dto.Type is null)
            return MeasureValue.Undefined;

        var type = Enum.Parse<MeasureValueType>(dto.Type);
        var timestamp = dto.Timestamp is not null
            ? DateTime.Parse(dto.Timestamp, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow;

        return type switch
        {
            MeasureValueType.Number when dto.Value is not null =>
                MeasureValue.FromValue(definition,
                    double.Parse(dto.Value, CultureInfo.InvariantCulture),
                    timestamp),

            MeasureValueType.String when dto.Value is not null =>
                new MeasureValue(dto.Value, timestamp),

            _ => MeasureValue.Undefined,
        };
    }

    private sealed class DefinitionDto
    {
        public string? Type { get; set; }
        public string? Attributes { get; set; }
        public string? QuantityType { get; set; }
        public string? Unit { get; set; }
        public double? Minimum { get; set; }
        public double? Maximum { get; set; }
        public int? Precision { get; set; }
        public double? TtlSeconds { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public List<string>? Tags { get; set; }
        public string? CreatedAt { get; set; }
    }

    private sealed class ValueDto
    {
        public string? Type { get; set; }
        public string? Value { get; set; }
        public string? Unit { get; set; }
        public string? QuantityType { get; set; }
        public string? Timestamp { get; set; }
    }
}
