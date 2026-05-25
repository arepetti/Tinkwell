namespace Tinkwell.Measures.History.Tests;

public sealed class HistoryAggregationTests
{
    /// <summary>
    /// Guards the aggregation names relied on by gRPC and queries. When adding a
    /// new <see cref="HistoryAggregation"/> member, extend this list — we
    /// intentionally avoid a fixed <c>Enum.GetValues().Length</c> check so the
    /// suite does not break when new kinds are added elsewhere.
    /// </summary>
    [Fact]
    public void All_enum_values_exist()
    {
        var names = Enum.GetNames<HistoryAggregation>();

        Assert.Contains(nameof(HistoryAggregation.None), names);
        Assert.Contains(nameof(HistoryAggregation.Average), names);
        Assert.Contains(nameof(HistoryAggregation.Min), names);
        Assert.Contains(nameof(HistoryAggregation.Max), names);
        Assert.Contains(nameof(HistoryAggregation.Sum), names);
        Assert.Contains(nameof(HistoryAggregation.Count), names);
        Assert.Contains(nameof(HistoryAggregation.First), names);
        Assert.Contains(nameof(HistoryAggregation.Last), names);
    }
}
