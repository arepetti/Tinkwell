namespace Tinkwell.Measures.History.Tests;

public sealed class MeasureHistoryResultTests
{
    [Fact]
    public void Construction_with_empty_points_list()
    {
        var result = new MeasureHistoryResult
        {
            Points = [],
            HasMore = false,
        };

        Assert.Empty(result.Points);
        Assert.False(result.HasMore);
    }

    [Fact]
    public void HasMore_defaults_false_when_unspecified()
    {
        var result = new MeasureHistoryResult { Points = [] };

        Assert.False(result.HasMore);
    }

    [Fact]
    public void Points_exposed_as_read_only_list()
    {
        var ts = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var p = new MeasureHistoryPoint { Name = "m", Timestamp = ts, NumericValue = 1 };
        var result = new MeasureHistoryResult { Points = [p] };

        Assert.IsAssignableFrom<IReadOnlyList<MeasureHistoryPoint>>(result.Points);
        Assert.Single(result.Points);
        Assert.Same(p, result.Points[0]);
    }
}
