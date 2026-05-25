namespace Tinkwell.Measures.History.Tests;

public sealed class MeasureHistoryQueryTests
{
    [Fact]
    public void Construction_with_only_Name_required_other_optionals_null()
    {
        var q = new MeasureHistoryQuery { Name = "signal" };

        Assert.Equal("signal", q.Name);
        Assert.Null(q.From);
        Assert.Null(q.To);
        Assert.Null(q.Limit);
        Assert.Null(q.Aggregation);
        Assert.Null(q.AggregationInterval);
    }

    [Fact]
    public void From_and_To_as_DateTime_values()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var q = new MeasureHistoryQuery
        {
            Name = "x",
            From = from,
            To = to,
        };

        Assert.Equal(from, q.From);
        Assert.Equal(to, q.To);
    }

    [Fact]
    public void Aggregation_with_interval()
    {
        var interval = TimeSpan.FromMinutes(5);

        var q = new MeasureHistoryQuery
        {
            Name = "x",
            Aggregation = HistoryAggregation.Average,
            AggregationInterval = interval,
        };

        Assert.Equal(HistoryAggregation.Average, q.Aggregation);
        Assert.Equal(interval, q.AggregationInterval);
    }

    [Fact]
    public void Limit_is_set()
    {
        var q = new MeasureHistoryQuery
        {
            Name = "x",
            Limit = 500,
        };

        Assert.Equal(500, q.Limit);
    }
}
