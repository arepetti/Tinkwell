namespace Tinkwell.Measures.History.Tests;

public sealed class MeasureHistoryPointTests
{
    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Construction_sets_numeric_string_opaque_unit_and_correlation()
    {
        var ts = Utc(2026, 4, 27);
        var opaque = new byte[] { 0xAB, 0xCD };

        var point = new MeasureHistoryPoint
        {
            Name = "sensor.temp",
            Timestamp = ts,
            NumericValue = 21.5,
            StringValue = "warn",
            OpaqueValue = opaque,
            Unit = "°C",
            CorrelationId = "corr-1",
        };

        Assert.Equal("sensor.temp", point.Name);
        Assert.Equal(ts, point.Timestamp);
        Assert.Equal(21.5, point.NumericValue);
        Assert.Equal("warn", point.StringValue);
        Assert.Same(opaque, point.OpaqueValue);
        Assert.Equal("°C", point.Unit);
        Assert.Equal("corr-1", point.CorrelationId);
    }

    [Fact]
    public void Construction_required_only_sets_name_and_timestamp_other_properties_null()
    {
        var ts = Utc(2026, 1, 2);

        var point = new MeasureHistoryPoint
        {
            Name = "x",
            Timestamp = ts,
        };

        Assert.Equal("x", point.Name);
        Assert.Equal(ts, point.Timestamp);
        Assert.Null(point.NumericValue);
        Assert.Null(point.StringValue);
        Assert.Null(point.OpaqueValue);
        Assert.Null(point.Unit);
        Assert.Null(point.CorrelationId);
    }

    [Fact]
    public void OpaqueValue_byte_array_round_trips_content()
    {
        var payload = new byte[] { 1, 2, 3, 255 };
        var point = new MeasureHistoryPoint
        {
            Name = "binary",
            Timestamp = DateTime.UtcNow,
            OpaqueValue = payload,
        };

        Assert.Equal(payload, point.OpaqueValue);
    }

    [Fact]
    public void Default_optional_properties_are_null_when_omitted()
    {
        var point = new MeasureHistoryPoint
        {
            Name = "n",
            Timestamp = DateTime.UtcNow,
        };

        Assert.Null(point.NumericValue);
        Assert.Null(point.StringValue);
        Assert.Null(point.OpaqueValue);
        Assert.Null(point.Unit);
        Assert.Null(point.CorrelationId);
    }

    [Fact]
    public void Record_equality_identical_points_are_equal()
    {
        var ts = Utc(2026, 3, 4);
        var a = new MeasureHistoryPoint
        {
            Name = "m",
            Timestamp = ts,
            NumericValue = 1,
            Unit = "u",
        };
        var b = new MeasureHistoryPoint
        {
            Name = "m",
            Timestamp = ts,
            NumericValue = 1,
            Unit = "u",
        };

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void With_expression_creates_copy_with_changed_property()
    {
        var ts = Utc(2026, 5, 1);
        var original = new MeasureHistoryPoint
        {
            Name = "p",
            Timestamp = ts,
            NumericValue = 10,
            CorrelationId = "c",
        };

        var updated = original with { NumericValue = 20 };

        Assert.Equal(10, original.NumericValue);
        Assert.Equal(20, updated.NumericValue);
        Assert.Equal(original.Name, updated.Name);
        Assert.Equal(original.Timestamp, updated.Timestamp);
        Assert.Equal(original.CorrelationId, updated.CorrelationId);
    }
}
