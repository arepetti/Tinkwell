using Tinkwell.Configuration;
using Tinkwell.Runlet.Measures.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Measures;

namespace Tinkwell.Runlet.Measures.Configuration.Tests;

public class MeasuresParserTests
{
    private readonly MeasuresParser _parser = new();

    private Task<MeasuresConfig> ParseFile(string relativePath)
    {
        var path = Path.Combine("TestFiles", relativePath);
        return _parser.LoadFileAsync(path);
    }

    private Task<MeasuresConfig> ParseFileLax(string relativePath)
    {
        var parser = new MeasuresParser(options: new ParserOptions { Lax = true });
        var path = Path.Combine("TestFiles", relativePath);
        return parser.LoadFileAsync(path);
    }

    // -----------------------------------------------------------------------
    // Basic parsing
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Basic_ParsesAllProperties()
    {
        var config = await ParseFile("basic.tw");

        var entry = Assert.Single(config.Measures);
        var def = entry.Definition;
        var meta = entry.Metadata;

        Assert.Equal("temperature", def.Name);
        Assert.Equal(MeasureType.Number, def.Type);
        Assert.Equal(MeasureAttributes.None, def.Attributes);
        Assert.Equal("Temperature", def.QuantityType);
        Assert.Equal("DegreeCelsius", def.Unit);
        Assert.Equal(-40, def.Minimum);
        Assert.Equal(85, def.Maximum);
        Assert.Equal(2, def.Precision);
        Assert.NotNull(def.Ttl);
        Assert.Equal(300, def.Ttl!.Value.TotalSeconds);

        Assert.Equal("Room temperature sensor", meta.Description);
        Assert.Equal("environment", meta.Category);
        Assert.Equal(3, meta.Tags.Count);
        Assert.Contains("indoor", meta.Tags);
        Assert.Contains("hvac", meta.Tags);
        Assert.Contains("sensor", meta.Tags);

        Assert.Null(entry.Value);
    }

    // -----------------------------------------------------------------------
    // Minimal measure (empty body)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Minimal_EmptyBody_DefaultValues()
    {
        var config = await ParseFile("minimal.tw");

        var entry = Assert.Single(config.Measures);
        var def = entry.Definition;

        Assert.Equal("simple-counter", def.Name);
        Assert.Equal(MeasureType.Number, def.Type);
        Assert.Equal(MeasureAttributes.None, def.Attributes);
        Assert.Equal("Scalar", def.QuantityType);
        Assert.Null(def.Unit);
        Assert.Null(def.Minimum);
        Assert.Null(def.Maximum);
        Assert.Null(def.Precision);
        Assert.Null(def.Ttl);
        Assert.Null(entry.Value);
    }

    // -----------------------------------------------------------------------
    // Constant measures (value = number)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Constant_IntegerValue_SetsConstantAttribute()
    {
        var config = await ParseFile("constant.tw");

        Assert.Equal(2, config.Measures.Count);

        var fw = config.Measures[0];
        Assert.Equal("firmware-version", fw.Definition.Name);
        Assert.Equal(MeasureAttributes.Constant, fw.Definition.Attributes);
        Assert.Equal("42", fw.Value);
    }

    [Fact]
    public async Task Constant_DoubleValue_SetsConstantAttribute()
    {
        var config = await ParseFile("constant.tw");

        var pi = config.Measures[1];
        Assert.Equal("pi", pi.Definition.Name);
        Assert.Equal(MeasureAttributes.Constant, pi.Definition.Attributes);
        Assert.NotNull(pi.Value);
        Assert.Contains("3.14159", pi.Value!);
        Assert.Equal(5, pi.Definition.Precision);
    }

    // -----------------------------------------------------------------------
    // Derived measures (value = expression)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Derived_ParenExpression_SetsDerivedAttribute()
    {
        var config = await ParseFile("derived.tw");

        var cpu = config.Measures[0];
        Assert.Equal("cpu-load", cpu.Definition.Name);
        Assert.Equal(MeasureAttributes.Derived, cpu.Definition.Attributes);
        Assert.Equal("get_sensor('cpu.load')", cpu.Value);
        Assert.Equal("Ratio", cpu.Definition.QuantityType);
        Assert.Equal("Percent", cpu.Definition.Unit);
        Assert.Equal(0, cpu.Definition.Minimum);
        Assert.Equal(100, cpu.Definition.Maximum);
        Assert.Equal(1, cpu.Definition.Precision);
    }

    [Fact]
    public async Task Derived_VerbatimExpression_SetsDerivedAttribute()
    {
        var config = await ParseFile("derived.tw");

        var avg = config.Measures[1];
        Assert.Equal("avg-temp", avg.Definition.Name);
        Assert.Equal(MeasureAttributes.Derived, avg.Definition.Attributes);
        Assert.Equal("(temperature_indoor + temperature_outdoor) / 2", avg.Value);
    }

    [Fact]
    public async Task Derived_StringValue_SetsDerivedAttribute()
    {
        var config = await ParseFile("derived.tw");

        var label = config.Measures[2];
        Assert.Equal("label", label.Definition.Name);
        Assert.Equal(MeasureAttributes.Derived, label.Definition.Attributes);
        Assert.Equal("fixed-label", label.Value);
    }

    // -----------------------------------------------------------------------
    // Plain measure (no value)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Plain_NoValue_NoneAttributes()
    {
        var config = await ParseFile("plain.tw");

        var entry = Assert.Single(config.Measures);
        Assert.Equal("humidity", entry.Definition.Name);
        Assert.Equal(MeasureAttributes.None, entry.Definition.Attributes);
        Assert.Null(entry.Value);
        Assert.Equal("RelativeHumidity", entry.Definition.QuantityType);
        Assert.Equal("Percent", entry.Definition.Unit);
    }

    // -----------------------------------------------------------------------
    // Multiple measures
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Multiple_ParsesAll()
    {
        var config = await ParseFile("multiple.tw");

        Assert.Equal(3, config.Measures.Count);
        Assert.Equal("temp-indoor", config.Measures[0].Definition.Name);
        Assert.Equal("temp-outdoor", config.Measures[1].Definition.Name);
        Assert.Equal("uptime", config.Measures[2].Definition.Name);
    }

    [Fact]
    public async Task Multiple_PreservesOrder()
    {
        var config = await ParseFile("multiple.tw");

        Assert.Equal("environment", config.Measures[0].Metadata.Category);
        Assert.Equal("environment", config.Measures[1].Metadata.Category);
        Assert.Equal(MeasureAttributes.None, config.Measures[2].Definition.Attributes);
        Assert.Equal("0", config.Measures[2].Value);
    }

    // -----------------------------------------------------------------------
    // Tags parsing
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Tags_CommaSeparated_ParsedAndTrimmed()
    {
        var config = await ParseFile("basic.tw");

        var tags = config.Measures[0].Metadata.Tags;
        Assert.Equal(3, tags.Count);
        Assert.Equal("indoor", tags[0]);
        Assert.Equal("hvac", tags[1]);
        Assert.Equal("sensor", tags[2]);
    }

    // -----------------------------------------------------------------------
    // All measures are MeasureType.Number
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AllMeasures_AreTypeNumber()
    {
        var config = await ParseFile("multiple.tw");

        foreach (var m in config.Measures)
            Assert.Equal(MeasureType.Number, m.Definition.Type);
    }

    // -----------------------------------------------------------------------
    // Error: unknown property
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UnknownProperty_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("unknown-property.tw"));

        Assert.Contains("Unknown property 'bogus'", ex.Message);
    }

    // -----------------------------------------------------------------------
    // Error: invalid unit
    // -----------------------------------------------------------------------

    [Fact]
    public async Task InvalidUnit_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("invalid-unit.tw"));

        Assert.Contains("NotARealUnit", ex.Message);
        Assert.Contains("Temperature", ex.Message);
    }

    // -----------------------------------------------------------------------
    // Error: duplicate name
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DuplicateName_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("duplicate-name.tw"));

        Assert.Contains("Duplicate", ex.Message);
        Assert.Contains("sensor-a", ex.Message);
    }

    // -----------------------------------------------------------------------
    // Error: nested blocks
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NestedBlocks_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("nested-block.tw"));

        Assert.Contains("may only contain nested 'signal' and 'on error' blocks", ex.Message);
    }

    // -----------------------------------------------------------------------
    // Numeric value without const (initial value, updatable)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NumericValue_WithoutConst_NoneAttributes()
    {
        var config = await ParseFile("initial-value.tw");

        var entry = Assert.Single(config.Measures);
        Assert.Equal("voltage", entry.Definition.Name);
        Assert.Equal(MeasureAttributes.None, entry.Definition.Attributes);
        Assert.Equal("220", entry.Value);
    }

    // -----------------------------------------------------------------------
    // Const validation errors
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Const_WithoutValue_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("const-no-value.tw"));

        Assert.Contains("const", ex.Message);
        Assert.Contains("no value", ex.Message);
    }

    [Fact]
    public async Task Const_WithExpression_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("const-with-expression.tw"));

        Assert.Contains("const", ex.Message);
        Assert.Contains("not a numeric literal", ex.Message);
    }

    // -----------------------------------------------------------------------
    // Quantity/Unit naming conventions
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("electric-potential", "ElectricPotential")]
    [InlineData("electric_potential", "ElectricPotential")]
    [InlineData("Electric Potential", "ElectricPotential")]
    [InlineData("electric potential", "ElectricPotential")]
    [InlineData("ElectricPotential", "ElectricPotential")]
    public void NormalizeToPascalCase_AllVariants(string input, string expected)
    {
        Assert.Equal(expected, MeasuresParser.NormalizeToPascalCase(input));
    }

    [Fact]
    public async Task NamingConventions_AllResolveToSameQuantityType()
    {
        var config = await ParseFile("naming-conventions.tw");

        Assert.Equal(5, config.Measures.Count);

        foreach (var entry in config.Measures)
            Assert.Equal("ElectricPotential", entry.Definition.QuantityType);
    }

    [Fact]
    public async Task NamingConventions_KebabUnit_Normalized()
    {
        var config = await ParseFile("naming-conventions.tw");

        Assert.Equal("Volt", config.Measures[0].Definition.Unit);
    }

    [Fact]
    public async Task NamingConventions_SnakeUnit_Normalized()
    {
        var config = await ParseFile("naming-conventions.tw");

        Assert.Equal("Volt", config.Measures[1].Definition.Unit);
    }

    [Fact]
    public async Task NamingConventions_SpacesUnit_Normalized()
    {
        var config = await ParseFile("naming-conventions.tw");

        Assert.Equal("Volt", config.Measures[2].Definition.Unit);
    }

    [Fact]
    public async Task NamingConventions_MixedCaseUnit_Normalized()
    {
        var config = await ParseFile("naming-conventions.tw");

        Assert.Equal("Volt", config.Measures[3].Definition.Unit);
    }

    [Fact]
    public async Task NamingConventions_PascalCase_Unchanged()
    {
        var config = await ParseFile("naming-conventions.tw");

        Assert.Equal("Volt", config.Measures[4].Definition.Unit);
    }

    // -----------------------------------------------------------------------
    // Error: unknown block type (strict mode)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UnknownBlockType_StrictMode_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("lax-mode.tw"));

        Assert.Contains("'runner'", ex.Message);
    }

    // -----------------------------------------------------------------------
    // Lax mode: skips unknown blocks
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LaxMode_SkipsUnknownBlocks()
    {
        var config = await ParseFileLax("lax-mode.tw");

        var entry = Assert.Single(config.Measures);
        Assert.Equal("temperature", entry.Definition.Name);
    }

    // -----------------------------------------------------------------------
    // on error: derived measure
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OnError_DerivedMeasure_ParsesWithRetry()
    {
        var config = await ParseFile("on-error-derived.tw");
        var entry = Assert.Single(config.Measures);

        Assert.Equal("power", entry.Definition.Name);
        Assert.Equal(MeasureAttributes.Derived, entry.Definition.Attributes);
        Assert.NotNull(entry.OnError);
        Assert.Equal(ErrorPolicyAction.ResumeNext, entry.OnError!.Action);
        Assert.NotNull(entry.OnError.Retry);
        Assert.Equal(2, entry.OnError.Retry!.Count);
        Assert.Equal(500, entry.OnError.Retry.DelayMs);
        Assert.Equal(1.0, entry.OnError.Retry.BackoffMultiplier);
    }
}
