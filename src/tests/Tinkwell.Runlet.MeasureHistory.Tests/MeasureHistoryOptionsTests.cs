namespace Tinkwell.Runlet.MeasureHistory.Tests;

public sealed class MeasureHistoryOptionsTests
{
    [Fact]
    public void Options_record_stores_values_correctly()
    {
        var opt = new MeasureHistoryOptions
        {
            Backend = "timescaledb",
            ConnectionString = "Host=localhost",
            BatchSize = 50,
            FlushIntervalMs = 250,
        };

        Assert.Equal("timescaledb", opt.Backend);
        Assert.Equal("Host=localhost", opt.ConnectionString);
        Assert.Equal(50, opt.BatchSize);
        Assert.Equal(250, opt.FlushIntervalMs);
    }

    [Fact]
    public void Default_numeric_members_are_zero_when_unset()
    {
        var opt = new MeasureHistoryOptions { Backend = "x" };

        Assert.Equal(0, opt.BatchSize);
        Assert.Equal(0, opt.FlushIntervalMs);
    }

    [Fact]
    public void Connection_string_defaults_null_when_omitted()
    {
        var opt = new MeasureHistoryOptions { Backend = "x" };

        Assert.Null(opt.ConnectionString);
    }
}
