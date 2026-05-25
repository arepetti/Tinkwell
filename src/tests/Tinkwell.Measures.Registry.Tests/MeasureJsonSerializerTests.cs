using System.Text.Json;
using Tinkwell.Measures;
using Tinkwell.Runlet.Measures.Registry;

namespace Tinkwell.Measures.Registry.Tests;

public class MeasureJsonSerializerTests
{
    [Fact]
    public void Definition_Roundtrip_PreservesAllFields()
    {
        var def = new MeasureDefinition
        {
            Name = "temperature",
            Type = MeasureType.Number,
            Attributes = MeasureAttributes.Constant | MeasureAttributes.System,
            QuantityType = "Temperature",
            Unit = "DegreeCelsius",
            Minimum = -40,
            Maximum = 85,
            Precision = 2,
            Ttl = TimeSpan.FromMinutes(5),
        };

        var meta = new MeasureMetadata
        {
            Description = "Room temperature",
            Category = "environment",
            Tags = ["indoor", "hvac"],
            CreatedAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
        };

        var json = MeasureJsonSerializer.SerializeDefinition(def, meta);
        var (resDef, resMeta) = MeasureJsonSerializer.DeserializeDefinition("temperature", json);

        Assert.Equal("temperature", resDef.Name);
        Assert.Equal(MeasureType.Number, resDef.Type);
        Assert.True(resDef.Attributes.HasFlag(MeasureAttributes.Constant));
        Assert.True(resDef.Attributes.HasFlag(MeasureAttributes.System));
        Assert.Equal("Temperature", resDef.QuantityType);
        Assert.Equal("DegreeCelsius", resDef.Unit);
        Assert.Equal(-40, resDef.Minimum);
        Assert.Equal(85, resDef.Maximum);
        Assert.Equal(2, resDef.Precision);
        Assert.NotNull(resDef.Ttl);
        Assert.Equal(300, resDef.Ttl!.Value.TotalSeconds);

        Assert.Equal("Room temperature", resMeta.Description);
        Assert.Equal("environment", resMeta.Category);
        Assert.Equal(2, resMeta.Tags.Count);
        Assert.Contains("indoor", resMeta.Tags);
        Assert.Contains("hvac", resMeta.Tags);
        Assert.Equal(meta.CreatedAt, resMeta.CreatedAt);
    }

    [Fact]
    public void Definition_Roundtrip_MinimalFields()
    {
        var def = new MeasureDefinition
        {
            Name = "simple",
            Type = MeasureType.String,
        };

        var json = MeasureJsonSerializer.SerializeDefinition(def, null);
        var (resDef, resMeta) = MeasureJsonSerializer.DeserializeDefinition("simple", json);

        Assert.Equal("simple", resDef.Name);
        Assert.Equal(MeasureType.String, resDef.Type);
        Assert.Null(resDef.Unit);
        Assert.Null(resDef.Minimum);
        Assert.Null(resDef.Maximum);
        Assert.Null(resDef.Precision);
        Assert.Null(resDef.Ttl);
        Assert.Null(resMeta.Description);
        Assert.Null(resMeta.Category);
        Assert.Empty(resMeta.Tags);
    }

    [Fact]
    public void Value_Roundtrip_NumberType()
    {
        var def = new MeasureDefinition
        {
            Name = "temp",
            Type = MeasureType.Number,
            QuantityType = "Temperature",
            Unit = "DegreeCelsius",
        };

        var timestamp = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var value = MeasureValue.FromValue(def, 23.5, timestamp);

        var json = MeasureJsonSerializer.SerializeValue(value);
        var deserialized = MeasureJsonSerializer.DeserializeValue(def, json);

        Assert.Equal(MeasureValueType.Number, deserialized.Type);
        Assert.Equal(23.5, deserialized.AsDouble(), 0.001);
        Assert.Equal(timestamp, deserialized.Timestamp);
    }

    [Fact]
    public void Value_Roundtrip_StringType()
    {
        var def = new MeasureDefinition
        {
            Name = "label",
            Type = MeasureType.String,
        };

        var timestamp = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var value = new MeasureValue("hello world", timestamp);

        var json = MeasureJsonSerializer.SerializeValue(value);
        var deserialized = MeasureJsonSerializer.DeserializeValue(def, json);

        Assert.Equal(MeasureValueType.String, deserialized.Type);
        Assert.Equal("hello world", deserialized.AsString());
        Assert.Equal(timestamp, deserialized.Timestamp);
    }

    [Fact]
    public void Value_Roundtrip_Scalar()
    {
        var def = new MeasureDefinition
        {
            Name = "count",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
        };

        var value = MeasureValue.FromValue(def, 42.0, DateTime.UtcNow);

        var json = MeasureJsonSerializer.SerializeValue(value);
        var deserialized = MeasureJsonSerializer.DeserializeValue(def, json);

        Assert.Equal(42.0, deserialized.AsDouble(), 0.001);
    }

    [Fact]
    public void Value_Deserialize_UndefinedType()
    {
        var def = new MeasureDefinition
        {
            Name = "test",
            Type = MeasureType.Number,
        };

        var json = """{"type":"Undefined"}""";
        var result = MeasureJsonSerializer.DeserializeValue(def, json);

        Assert.Equal(MeasureValueType.Undefined, result.Type);
    }

    /// <summary>
    /// Malformed JSON must surface as <see cref="JsonException"/>, not as silent bad state.
    /// </summary>
    [Fact]
    public void Value_Deserialize_MalformedJson_ThrowsJsonException()
    {
        var def = new MeasureDefinition { Name = "n", Type = MeasureType.Number };

        Assert.Throws<JsonException>(() => MeasureJsonSerializer.DeserializeValue(def, "{ not json"));
    }

    [Fact]
    public void Definition_Deserialize_MalformedJson_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => MeasureJsonSerializer.DeserializeDefinition("m", "}]"));
    }

    /// <summary>
    /// When <c>type</c> is absent or null, <see cref="MeasureJsonSerializer.DeserializeValue"/>
    /// treats the payload as <see cref="MeasureValueType.Undefined"/> (avoids conflating
    /// missing metadata with a numeric value).
    /// </summary>
    [Fact]
    public void Value_Deserialize_MissingTypeProperty_ReturnsUndefined()
    {
        var def = new MeasureDefinition
        {
            Name = "n",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
        };

        var json = """{"value":"99","timestamp":"2026-01-01T00:00:00Z"}""";
        var result = MeasureJsonSerializer.DeserializeValue(def, json);

        Assert.Equal(MeasureValueType.Undefined, result.Type);
    }

    [Fact]
    public void Value_Deserialize_ExplicitNullType_ReturnsUndefined()
    {
        var def = new MeasureDefinition { Name = "n", Type = MeasureType.Number };

        var json = """{"type":null}""";
        var result = MeasureJsonSerializer.DeserializeValue(def, json);

        Assert.Equal(MeasureValueType.Undefined, result.Type);
    }

    /// <summary>
    /// Partial definition JSON (only camelCase fields the serializer would emit) should still
    /// round-trip: missing optional fields use defaults (type Number, name from key parameter).
    /// </summary>
    [Fact]
    public void Definition_Deserialize_MinimalJson_UsesNameParameterAndNumberDefault()
    {
        const string json = "{}";
        var (resDef, resMeta) = MeasureJsonSerializer.DeserializeDefinition("from_key", json);

        Assert.Equal("from_key", resDef.Name);
        Assert.Equal(MeasureType.Number, resDef.Type);
        Assert.Equal("Scalar", resDef.QuantityType);
    }
}
