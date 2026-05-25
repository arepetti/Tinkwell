namespace Tinkwell.Measures.History.Tests;

public sealed class MeasureDefinitionSnapshotTests
{
    [Fact]
    public void Construction_with_all_properties_round_trips()
    {
        var tags = new[] { "a", "b" };
        var snap = new MeasureDefinitionSnapshot
        {
            Name = "temp",
            Type = "Number",
            QuantityType = "temperature",
            Unit = "K",
            Minimum = 0,
            Maximum = 100,
            Precision = 2,
            Description = "desc",
            Category = "sensors",
            Tags = tags,
        };

        Assert.Equal("temp", snap.Name);
        Assert.Equal("Number", snap.Type);
        Assert.Equal("temperature", snap.QuantityType);
        Assert.Equal("K", snap.Unit);
        Assert.Equal(0, snap.Minimum);
        Assert.Equal(100, snap.Maximum);
        Assert.Equal(2, snap.Precision);
        Assert.Equal("desc", snap.Description);
        Assert.Equal("sensors", snap.Category);
        Assert.Equal(tags, snap.Tags);
    }

    [Fact]
    public void Default_Tags_is_empty_list()
    {
        var snap = new MeasureDefinitionSnapshot
        {
            Name = "x",
            Type = "String",
        };

        Assert.NotNull(snap.Tags);
        Assert.Empty(snap.Tags);
    }

    [Fact]
    public void Optional_properties_default_to_null_when_omitted()
    {
        var snap = new MeasureDefinitionSnapshot
        {
            Name = "x",
            Type = "Number",
        };

        Assert.Null(snap.QuantityType);
        Assert.Null(snap.Unit);
        Assert.Null(snap.Minimum);
        Assert.Null(snap.Maximum);
        Assert.Null(snap.Precision);
        Assert.Null(snap.Description);
        Assert.Null(snap.Category);
    }

    [Fact]
    public void Record_equality_uses_tags_list_reference()
    {
        var sharedTags = new[] { "t1" };
        var a = new MeasureDefinitionSnapshot
        {
            Name = "m",
            Type = "Number",
            Tags = sharedTags,
        };
        var b = new MeasureDefinitionSnapshot
        {
            Name = "m",
            Type = "Number",
            Tags = sharedTags,
        };
        var c = new MeasureDefinitionSnapshot
        {
            Name = "m",
            Type = "Number",
            Tags = new[] { "t1" },
        };

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);

        var d = new MeasureDefinitionSnapshot
        {
            Name = "m",
            Type = "Number",
            Tags = new[] { "t2" },
        };
        Assert.NotEqual(a, d);
    }
}
