using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Integration;

namespace Tinkwell.Integrations.Tests;

public class BindingParameterResolverTests
{
    private static readonly IReadOnlyDictionary<string, object?> NoParams = new Dictionary<string, object?>();

    [Fact]
    public async Task ResolveOptionalAsync_MissingKey_ReturnsNull()
    {
        var r = await BindingParameterResolver.ResolveOptionalAsync(
            "k",
            BindingParameterSet.Empty,
            new PassthroughEvaluator(),
            NoParams,
            CancellationToken.None);
        Assert.Null(r);
    }

    [Fact]
    public async Task ResolveOptionalAsync_StringValue_ReturnsValue()
    {
        var p = new BindingParameterSet(
            new Dictionary<string, ConfigValue> { ["k"] = new StringValue("v") },
            new Dictionary<string, IReadOnlyDictionary<string, ConfigValue>>());
        var r = await BindingParameterResolver.ResolveOptionalAsync("k", p, new PassthroughEvaluator(), NoParams, CancellationToken.None);
        Assert.Equal("v", r);
    }

    [Fact]
    public async Task ResolveOptionalAsync_ExpressionValue_UsesEvaluator()
    {
        var p = new BindingParameterSet(
            new Dictionary<string, ConfigValue> { ["k"] = new ExpressionValue("1+1") },
            new Dictionary<string, IReadOnlyDictionary<string, ConfigValue>>());
        var r = await BindingParameterResolver.ResolveOptionalAsync("k", p, new PassthroughEvaluator(), NoParams, CancellationToken.None);
        Assert.Equal("1+1", r);
    }

    [Fact]
    public async Task ResolveOptionalAsync_LongValue_UsesToString()
    {
        var p = new BindingParameterSet(
            new Dictionary<string, ConfigValue> { ["k"] = new LongValue(99L) },
            new Dictionary<string, IReadOnlyDictionary<string, ConfigValue>>());
        var r = await BindingParameterResolver.ResolveOptionalAsync("k", p, new PassthroughEvaluator(), NoParams, CancellationToken.None);
        Assert.Equal("99", r);
    }

    [Fact]
    public async Task ResolveOptionalAsync_DoubleValue_UsesToString()
    {
        var p = new BindingParameterSet(
            new Dictionary<string, ConfigValue> { ["k"] = new DoubleValue(3.5) },
            new Dictionary<string, IReadOnlyDictionary<string, ConfigValue>>());
        var r = await BindingParameterResolver.ResolveOptionalAsync("k", p, new PassthroughEvaluator(), NoParams, CancellationToken.None);
        Assert.Equal("3.5", r);
    }

    [Fact]
    public async Task ResolveOptionalAsync_BoolValue_UsesToString()
    {
        var p = new BindingParameterSet(
            new Dictionary<string, ConfigValue> { ["k"] = BoolValue.True },
            new Dictionary<string, IReadOnlyDictionary<string, ConfigValue>>());
        var r = await BindingParameterResolver.ResolveOptionalAsync("k", p, new PassthroughEvaluator(), NoParams, CancellationToken.None);
        Assert.Equal("true", r);
    }

    [Fact]
    public async Task ResolveRequiredAsync_Missing_ThrowsWithBindingName()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => BindingParameterResolver.ResolveRequiredAsync(
            "name",
            "Measure",
            BindingParameterSet.Empty,
            new PassthroughEvaluator(),
            NoParams,
            CancellationToken.None));
        Assert.Contains("Measure", ex.Message, StringComparison.Ordinal);
        Assert.Contains("name", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveRequiredAsync_Present_ReturnsValue()
    {
        var p = new BindingParameterSet(
            new Dictionary<string, ConfigValue> { ["name"] = new StringValue("m1") },
            new Dictionary<string, IReadOnlyDictionary<string, ConfigValue>>());
        var r = await BindingParameterResolver.ResolveRequiredAsync("name", "Measure", p, new PassthroughEvaluator(), NoParams, CancellationToken.None);
        Assert.Equal("m1", r);
    }

    [Fact]
    public async Task ResolveConfigValueAsync_Expression()
    {
        var s = await BindingParameterResolver.ResolveConfigValueAsync(
            new ExpressionValue("e"),
            new PassthroughEvaluator(),
            NoParams,
            CancellationToken.None);
        Assert.Equal("e", s);
    }

    [Fact]
    public async Task ResolveConfigValueAsync_String()
    {
        var s = await BindingParameterResolver.ResolveConfigValueAsync(
            new StringValue("plain"),
            new PassthroughEvaluator(),
            NoParams,
            CancellationToken.None);
        Assert.Equal("plain", s);
    }

    [Theory]
    [MemberData(nameof(ConfigValueToStringCases))]
    public async Task ResolveConfigValueAsync_OtherConfigValuesUseToString(ConfigValue value, string expected)
    {
        var s = await BindingParameterResolver.ResolveConfigValueAsync(value, new PassthroughEvaluator(), NoParams, CancellationToken.None);
        Assert.Equal(expected, s);
    }

    public static TheoryData<ConfigValue, string> ConfigValueToStringCases()
    {
        return new TheoryData<ConfigValue, string>
        {
            { new LongValue(-7), "-7" },
            { new DoubleValue(0.25), "0.25" },
            { BoolValue.False, "false" },
        };
    }
}
