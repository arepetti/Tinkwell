namespace Tinkwell.Measures.History.Tests;

public sealed class MeasureDataRangeTests
{
    [Fact]
    public void Default_has_null_earliest_and_latest()
    {
        var range = new MeasureDataRange();

        Assert.Null(range.Earliest);
        Assert.Null(range.Latest);
    }

    [Fact]
    public void Dates_roundtrip_via_init_properties()
    {
        var earliest = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var latest = new DateTime(2026, 4, 27, 23, 0, 0, DateTimeKind.Utc);

        var range = new MeasureDataRange { Earliest = earliest, Latest = latest };

        Assert.Equal(earliest, range.Earliest);
        Assert.Equal(latest, range.Latest);
    }

    [Fact]
    public void Record_equality_holds_for_same_dates()
    {
        var ts = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var a = new MeasureDataRange { Earliest = ts, Latest = ts };
        var b = new MeasureDataRange { Earliest = ts, Latest = ts };

        Assert.Equal(a, b);
    }

    [Fact]
    public void Record_equality_holds_for_both_null()
    {
        Assert.Equal(new MeasureDataRange(), new MeasureDataRange());
    }
}
