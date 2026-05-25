using Tinkwell.Measures;
using UnitsNet;

namespace Tinkwell.Measures.Tests;

public class MeasureTests
{
    [Fact]
    public void IsExpired_WithoutTtl_ReturnsFalse()
    {
        var m = CreateMeasure(ttl: null, timestamp: DateTime.UtcNow.AddHours(-1));
        Assert.False(m.IsExpired);
    }

    [Fact]
    public void IsExpired_WithinTtl_ReturnsFalse()
    {
        var m = CreateMeasure(
            ttl: TimeSpan.FromMinutes(5),
            timestamp: DateTime.UtcNow);

        Assert.False(m.IsExpired);
    }

    [Fact]
    public void IsExpired_PastTtl_ReturnsTrue()
    {
        var m = CreateMeasure(
            ttl: TimeSpan.FromMinutes(5),
            timestamp: DateTime.UtcNow.AddMinutes(-10));

        Assert.True(m.IsExpired);
    }

    private static Measure CreateMeasure(TimeSpan? ttl, DateTime timestamp)
    {
        var def = new MeasureDefinition
        {
            Name = "test",
            Type = MeasureType.Number,
            Ttl = ttl,
        };

        return new Measure
        {
            Definition = def,
            Metadata = new MeasureMetadata(),
            Value = new MeasureValue(Scalar.FromAmount(42), timestamp),
        };
    }
}
