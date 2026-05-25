using Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Runlet.MeasureHistory.Tests;

public sealed class MeasureHistoryWorkerTests
{
    [Fact]
    public void ToHistoryPoint_numeric_sets_NumericValue_and_Unit_leaves_StringValue_null()
    {
        var ev = new MeasureEvent
        {
            Name = "t",
            NewValue = new MeasureValueProto
            {
                Type = "Number",
                NumericValue = 3.25,
                Unit = "V",
            },
        };

        var point = MeasureHistoryWorker.ToHistoryPoint(ev);

        Assert.Equal("t", point.Name);
        Assert.Equal(3.25, point.NumericValue);
        Assert.Null(point.StringValue);
        Assert.Equal("V", point.Unit);
    }

    [Fact]
    public void ToHistoryPoint_string_sets_StringValue_leaves_NumericValue_null()
    {
        var ev = new MeasureEvent
        {
            Name = "s",
            NewValue = new MeasureValueProto
            {
                Type = "String",
                StringValue = "hello",
            },
        };

        var point = MeasureHistoryWorker.ToHistoryPoint(ev);

        Assert.Equal("hello", point.StringValue);
        Assert.Null(point.NumericValue);
        Assert.Null(point.Unit);
    }

    [Fact]
    public void ToHistoryPoint_Undefined_type_leaves_both_values_null()
    {
        var ev = new MeasureEvent
        {
            Name = "u",
            NewValue = new MeasureValueProto { Type = "Undefined" },
        };

        var point = MeasureHistoryWorker.ToHistoryPoint(ev);

        Assert.Null(point.NumericValue);
        Assert.Null(point.StringValue);
    }

    [Fact]
    public void ToHistoryPoint_null_NewValue_leaves_both_values_null()
    {
        var ev = new MeasureEvent { Name = "n", NewValue = null };

        var point = MeasureHistoryWorker.ToHistoryPoint(ev);

        Assert.Null(point.NumericValue);
        Assert.Null(point.StringValue);
    }

    [Fact]
    public void ToHistoryPoint_empty_type_treated_as_undefined()
    {
        var ev = new MeasureEvent
        {
            Name = "e",
            NewValue = new MeasureValueProto { Type = "", NumericValue = 99, StringValue = "x" },
        };

        var point = MeasureHistoryWorker.ToHistoryPoint(ev);

        Assert.Null(point.NumericValue);
        Assert.Null(point.StringValue);
    }

    [Fact]
    public void ToHistoryPoint_numeric_empty_unit_becomes_null()
    {
        var ev = new MeasureEvent
        {
            Name = "t",
            NewValue = new MeasureValueProto
            {
                Type = "Number",
                NumericValue = 1,
                Unit = "",
            },
        };

        var point = MeasureHistoryWorker.ToHistoryPoint(ev);

        Assert.Null(point.Unit);
    }

    [Fact]
    public void ToHistoryPoint_timestamp_is_set_not_default()
    {
        var before = DateTime.UtcNow;
        var point = MeasureHistoryWorker.ToHistoryPoint(new MeasureEvent
        {
            Name = "x",
            NewValue = new MeasureValueProto { Type = "Number", NumericValue = 1 },
        });
        var after = DateTime.UtcNow;

        Assert.NotEqual(default, point.Timestamp);
        Assert.InRange(point.Timestamp, before, after);
    }

    [Fact]
    public void ToSnapshot_maps_all_fields_from_definition_and_metadata()
    {
        var def = new MeasureDefinitionProto
        {
            Name = "m1",
            Type = "Number",
            QuantityType = "pressure",
            Unit = "Pa",
            Minimum = -1,
            Maximum = 10,
            Precision = 3,
        };

        var meta = new MeasureMetadataProto
        {
            Description = "d",
            Category = "c",
        };

        var proto = new MeasureProto { Definition = def, Metadata = meta };

        var snap = MeasureHistoryWorker.ToSnapshot(proto);

        Assert.Equal("m1", snap.Name);
        Assert.Equal("Number", snap.Type);
        Assert.Equal("pressure", snap.QuantityType);
        Assert.Equal("Pa", snap.Unit);
        Assert.Equal(-1, snap.Minimum);
        Assert.Equal(10, snap.Maximum);
        Assert.Equal(3, snap.Precision);
        Assert.Equal("d", snap.Description);
        Assert.Equal("c", snap.Category);
        Assert.Empty(snap.Tags);
    }

    [Fact]
    public void ToSnapshot_empty_strings_become_null_for_QuantityType_Unit_Description_Category()
    {
        var def = new MeasureDefinitionProto
        {
            Name = "m2",
            Type = "String",
            QuantityType = "",
            Unit = "",
        };
        var meta = new MeasureMetadataProto
        {
            Description = "",
            Category = "",
        };

        var snap = MeasureHistoryWorker.ToSnapshot(new MeasureProto { Definition = def, Metadata = meta });

        Assert.Null(snap.QuantityType);
        Assert.Null(snap.Unit);
        Assert.Null(snap.Description);
        Assert.Null(snap.Category);
    }

    [Fact]
    public void ToSnapshot_optional_fields_Minimum_Maximum_Precision_use_HasXxx_checks()
    {
        var def = new MeasureDefinitionProto
        {
            Name = "m3",
            Type = "Number",
        };

        var snap = MeasureHistoryWorker.ToSnapshot(new MeasureProto
        {
            Definition = def,
            Metadata = new MeasureMetadataProto(),
        });

        Assert.Null(snap.Minimum);
        Assert.Null(snap.Maximum);
        Assert.Null(snap.Precision);
    }

    [Fact]
    public void ToSnapshot_tags_empty_when_no_tags()
    {
        var def = new MeasureDefinitionProto { Name = "m5", Type = "Number" };
        var snap = MeasureHistoryWorker.ToSnapshot(new MeasureProto
        {
            Definition = def,
            Metadata = new MeasureMetadataProto(),
        });

        Assert.Empty(snap.Tags);
    }

    [Fact]
    public void ToSnapshot_tags_populated_when_present()
    {
        var def = new MeasureDefinitionProto { Name = "m4", Type = "Number" };
        var meta = new MeasureMetadataProto();
        meta.Tags.Add("one");
        meta.Tags.Add("two");

        var snap = MeasureHistoryWorker.ToSnapshot(new MeasureProto { Definition = def, Metadata = meta });

        Assert.Equal(["one", "two"], snap.Tags);
    }
}
