using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class PayloadValueEdgeCaseTests
{
    [Fact]
    public void FromFloat_MaxValue()
    {
        var v = PayloadValue.FromFloat(double.MaxValue);
        Assert.Equal(double.MaxValue, v.AsDouble());
    }

    [Fact]
    public void FromFloat_MinValue()
    {
        var v = PayloadValue.FromFloat(double.MinValue);
        Assert.Equal(double.MinValue, v.AsDouble());
    }

    [Fact]
    public void FromFloat_NaN()
    {
        var v = PayloadValue.FromFloat(double.NaN);
        Assert.True(double.IsNaN(v.AsDouble()));
    }

    [Fact]
    public void FromFloat_PositiveInfinity()
    {
        var v = PayloadValue.FromFloat(double.PositiveInfinity);
        Assert.True(double.IsPositiveInfinity(v.AsDouble()));
    }

    [Fact]
    public void FromFloat_NegativeInfinity()
    {
        var v = PayloadValue.FromFloat(double.NegativeInfinity);
        Assert.True(double.IsNegativeInfinity(v.AsDouble()));
    }

    [Fact]
    public void FromInteger_MaxLong()
    {
        var v = PayloadValue.FromInteger(long.MaxValue);
        Assert.Equal(long.MaxValue, v.AsLong());
    }

    [Fact]
    public void FromInteger_MinLong()
    {
        var v = PayloadValue.FromInteger(long.MinValue);
        Assert.Equal(long.MinValue, v.AsLong());
    }

    [Fact]
    public void FromString_NullToString_ReturnsEmpty()
    {
        Assert.Equal("", PayloadValue.Empty.AsString());
    }

    [Fact]
    public void FromString_EmptyString()
    {
        var v = PayloadValue.FromString("");
        Assert.Equal("", v.AsString());
    }

    [Fact]
    public void AsDouble_FromNone_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PayloadValue.Empty.AsDouble());
    }

    [Fact]
    public void AsLong_FromNone_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PayloadValue.Empty.AsLong());
    }

    [Fact]
    public void AsBoolean_FromNone_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PayloadValue.Empty.AsBoolean());
    }

    [Fact]
    public void AsLong_FromString_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PayloadValue.FromString("text").AsLong());
    }

    [Fact]
    public void FromBoolean_AsDouble_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PayloadValue.FromBoolean(true).AsDouble());
    }

    [Fact]
    public void FromFloat_AsLong_Truncates_Negative()
    {
        var v = PayloadValue.FromFloat(-3.7);
        Assert.Equal(-3, v.AsLong());
    }

    [Fact]
    public void FromFloat_AsBoolean_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PayloadValue.FromFloat(1.0).AsBoolean());
    }

    [Fact]
    public void FromOpaque_AsString_ReturnsNonEmptyDiagnosticString()
    {
        var v = PayloadValue.FromOpaque([1, 2, 3]);
        var str = v.AsString();
        Assert.False(string.IsNullOrEmpty(str));
    }

    [Fact]
    public void FromTime_AsString_ContainsDate()
    {
        var ts = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var v = PayloadValue.FromTime(ts);
        var str = v.AsString();
        Assert.Contains("2025", str);
    }

    [Fact]
    public void RecordEquality_SameTypeAndValue()
    {
        var a = PayloadValue.FromFloat(42.0);
        var b = PayloadValue.FromFloat(42.0);
        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentValues()
    {
        Assert.NotEqual(PayloadValue.FromFloat(1.0), PayloadValue.FromFloat(2.0));
    }

    [Fact]
    public void RecordEquality_DifferentTypes()
    {
        Assert.NotEqual(PayloadValue.FromFloat(42.0), PayloadValue.FromInteger(42));
    }
}
