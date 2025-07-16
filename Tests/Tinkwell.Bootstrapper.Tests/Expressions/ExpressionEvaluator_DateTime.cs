using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Bootstrapper.Tests.Expressions;

public class ExpressionEvaluator_DateTime
{
    private readonly ExpressionEvaluator _evaluator = new();

    [Fact]
    public void Now_ReturnsCurrentUtcTime()
    {
        var date = (DateTime?)_evaluator.Evaluate("now()", null);
        Assert.NotNull(date);
        Assert.True(date?.Kind == DateTimeKind.Utc);
        Assert.True(DateTime.UtcNow - date < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ParseDate_ParsesDateCorrectly()
    {
        var date = (DateTime?)_evaluator.Evaluate("parse_date('2023-01-15T10:30:00Z')", null);
        Assert.NotNull(date);
        Assert.Equal(new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc), date);
    }

    [Fact]
    public void FormatDate_FormatsDateCorrectly()
    {
        var parameters = new { date = new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc) };
        Assert.Equal("2023-01-15", _evaluator.Evaluate("format_date(date, 'yyyy-MM-dd')", parameters));
    }

    [Fact]
    public void DateDiff_ReturnsCorrectTimeSpan()
    {
        var parameters = new
        {
            date1 = new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            date2 = new DateTime(2023, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        };
        Assert.Equal(TimeSpan.FromMinutes(30), _evaluator.Evaluate("date_diff(date1, date2)", parameters));
    }

    [Fact]
    public void DateAdd_ReturnsCorrectDateTime()
    {
        var parameters = new
        {
            date = new DateTime(2023, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            timespan = TimeSpan.FromHours(2)
        };
        Assert.Equal(new DateTime(2023, 1, 15, 12, 0, 0, DateTimeKind.Utc), _evaluator.Evaluate("date_add(date, timespan)", parameters));
    }

    [Theory]
    [InlineData("year", 2023)]
    [InlineData("month", 1)]
    [InlineData("day", 15)]
    [InlineData("hour", 10)]
    [InlineData("minute", 30)]
    [InlineData("second", 0)]
    public void DatePart_ReturnsCorrectValue(string part, int expectedValue)
    {
        var parameters = new { date = new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc) };
        Assert.Equal(expectedValue, _evaluator.Evaluate($"{part}(date)", parameters));
    }

    [Theory]
    [InlineData("1d", 1, 0, 0, 0)]
    [InlineData("2h", 0, 2, 0, 0)]
    [InlineData("30m", 0, 0, 30, 0)]
    [InlineData("45s", 0, 0, 0, 45)]
    [InlineData("00:01:00", 0, 0, 1, 0)]
    public void ParseTimespan_ParsesCorrectly(string timespanStr, int days, int hours, int minutes, int seconds)
    {
        var expected = new TimeSpan(days, hours, minutes, seconds);
        Assert.Equal(expected, _evaluator.Evaluate($"parse_timespan('{timespanStr}')", null));
    }

    [Fact]
    public void TimespanAdd_ReturnsCorrectTimeSpan()
    {
        var parameters = new
        {
            ts1 = TimeSpan.FromHours(1),
            ts2 = TimeSpan.FromMinutes(30)
        };
        Assert.Equal(TimeSpan.FromHours(1.5), _evaluator.Evaluate("timespan_add(ts1, ts2)", parameters));
    }

    [Fact]
    public void TimespanDiff_ReturnsCorrectTimeSpan()
    {
        var parameters = new
        {
            ts1 = TimeSpan.FromHours(2),
            ts2 = TimeSpan.FromMinutes(30)
        };
        Assert.Equal(TimeSpan.FromHours(1.5), _evaluator.Evaluate("timespan_diff(ts1, ts2)", parameters));
    }

    [Fact]
    public void Ago_ReturnsCorrectDateTime()
    {
        var result = (DateTime?)_evaluator.Evaluate("ago(timespan)", new { timespan = TimeSpan.FromHours(1) });
        Assert.NotNull(result);
        Assert.True(DateTime.UtcNow - result > TimeSpan.FromMinutes(59));
        Assert.True(DateTime.UtcNow - result < TimeSpan.FromMinutes(61));
    }

    [Fact]
    public void FromNow_ReturnsCorrectDateTime()
    {
        var result = (DateTime?)_evaluator.Evaluate("from_now(timespan)", new { timespan = TimeSpan.FromHours(1) });
        Assert.NotNull(result);
        Assert.True(result - DateTime.UtcNow > TimeSpan.FromMinutes(59));
        Assert.True(result - DateTime.UtcNow < TimeSpan.FromMinutes(61));
    }
}
