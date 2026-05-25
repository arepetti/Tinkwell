using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Integration;
using Tinkwell.Integration.Store;

namespace Tinkwell.Integrations.Tests;

public class StoreBindingTests
{
    private static readonly IReadOnlyDictionary<string, object?> NoParams = new Dictionary<string, object?>();

    [Fact]
    public async Task ResolveTtlSeconds_LongValue()
    {
        var p = new BindingParameterSet(
            new Dictionary<string, ConfigValue> { ["ttl"] = new LongValue(120L) },
            new Dictionary<string, IReadOnlyDictionary<string, ConfigValue>>());
        var ttl = await StoreBinding.ResolveTtlSecondsAsync(p, new PassthroughEvaluator(), NoParams, CancellationToken.None);
        Assert.Equal(120, ttl);
    }

    [Fact]
    public async Task ResolveTtlSeconds_StringValue()
    {
        var p = new BindingParameterSet(
            new Dictionary<string, ConfigValue> { ["ttl"] = new StringValue("30") },
            new Dictionary<string, IReadOnlyDictionary<string, ConfigValue>>());
        var ttl = await StoreBinding.ResolveTtlSecondsAsync(p, new PassthroughEvaluator(), NoParams, CancellationToken.None);
        Assert.Equal(30, ttl);
    }

    [Fact]
    public async Task ResolveTtlSeconds_Expression_Passthrough()
    {
        var p = new BindingParameterSet(
            new Dictionary<string, ConfigValue> { ["ttl"] = new ExpressionValue("60") },
            new Dictionary<string, IReadOnlyDictionary<string, ConfigValue>>());
        var ttl = await StoreBinding.ResolveTtlSecondsAsync(p, new PassthroughEvaluator(), NoParams, CancellationToken.None);
        Assert.Equal(60, ttl);
    }

    [Fact]
    public async Task ResolveTtlSeconds_Missing_IsZero()
    {
        var ttl = await StoreBinding.ResolveTtlSecondsAsync(
            BindingParameterSet.Empty,
            new PassthroughEvaluator(),
            NoParams,
            CancellationToken.None);
        Assert.Equal(0, ttl);
    }
}
