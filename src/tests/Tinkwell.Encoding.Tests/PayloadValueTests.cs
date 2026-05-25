using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class PayloadValueTests
{
    [Fact]
    public void FromFloat_AsDouble_Roundtrips()
    {
        var value = PayloadValue.FromFloat(42.5);
        Assert.Equal(42.5, value.AsDouble());
        Assert.Equal(PayloadType.Float, value.Type);
    }

    [Fact]
    public void FromInteger_AsLong_Roundtrips()
    {
        var value = PayloadValue.FromInteger(42);
        Assert.Equal(42, value.AsLong());
        Assert.Equal(PayloadType.Integer, value.Type);
    }

    [Fact]
    public void FromInteger_AsDouble_Converts()
    {
        var value = PayloadValue.FromInteger(42);
        Assert.Equal(42.0, value.AsDouble());
    }

    [Fact]
    public void FromFloat_AsLong_Truncates()
    {
        var value = PayloadValue.FromFloat(42.9);
        Assert.Equal(42, value.AsLong());
    }

    [Fact]
    public void FromString_AsString_Roundtrips()
    {
        var value = PayloadValue.FromString("hello");
        Assert.Equal("hello", value.AsString());
    }

    [Fact]
    public void FromBoolean_AsBoolean_Roundtrips()
    {
        Assert.True(PayloadValue.FromBoolean(true).AsBoolean());
        Assert.False(PayloadValue.FromBoolean(false).AsBoolean());
    }

    [Fact]
    public void FromInteger_AsBoolean_NonZeroIsTrue()
    {
        Assert.True(PayloadValue.FromInteger(1).AsBoolean());
        Assert.False(PayloadValue.FromInteger(0).AsBoolean());
    }

    [Fact]
    public void Empty_IsNoneType()
    {
        Assert.Equal(PayloadType.None, PayloadValue.Empty.Type);
        Assert.Null(PayloadValue.Empty.RawValue);
    }

    [Fact]
    public void FromOpaque_PreservesBytes()
    {
        var data = new byte[] { 1, 2, 3 };
        var value = PayloadValue.FromOpaque(data);
        Assert.Equal(data, (byte[])value.RawValue!);
    }

    [Fact]
    public void FromTime_PreservesTimestamp()
    {
        var ts = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var value = PayloadValue.FromTime(ts);
        Assert.Equal(ts, (DateTimeOffset)value.RawValue!);
    }

    [Fact]
    public void AsDouble_FromString_Throws()
    {
        var value = PayloadValue.FromString("text");
        Assert.Throws<InvalidOperationException>(() => value.AsDouble());
    }

    [Fact]
    public void AsBoolean_FromString_Throws()
    {
        var value = PayloadValue.FromString("text");
        Assert.Throws<InvalidOperationException>(() => value.AsBoolean());
    }
}
