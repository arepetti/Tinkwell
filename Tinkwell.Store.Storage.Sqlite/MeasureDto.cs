using Tinkwell.Measures;

namespace Tinkwell.Store.Storage.Sqlite;

sealed class MeasureDto
{
    public required string Name { get; set; }
    public int Type { get; set; }
    public string? StringValue { get; set; }
    public double? DoubleValue { get; set; }
    public long? Timestamp { get; set; }

    public MeasureValue ToMeasureValue(MeasureDefinition definition)
    {
        var timestamp = Timestamp.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(Timestamp.Value).DateTime : DateTime.UtcNow;
        return (MeasureValueType)Type switch
        {
            MeasureValueType.String => new MeasureValue(StringValue ?? "", timestamp),
            MeasureValueType.Number => new MeasureValue(Quant.Parse(definition.QuantityType, definition.Unit ?? ""), timestamp),
            _ => MeasureValue.Undefined,
        };
    }
}
