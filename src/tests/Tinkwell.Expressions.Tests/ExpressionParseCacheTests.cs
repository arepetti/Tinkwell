using System.Diagnostics.Metrics;
using Tinkwell.Expressions;

namespace Tinkwell.Expressions.Tests;

[Collection("ParseCache")]
public sealed class ExpressionParseCacheTests : IDisposable
{
    private readonly ExpressionEvaluator _evaluator = new(functions: null);
    private readonly int _originalCapacity;

    public ExpressionParseCacheTests()
    {
        _originalCapacity = ExpressionParseCache.Capacity;
        ExpressionParseCache.Clear();
    }

    public void Dispose()
    {
        ExpressionParseCache.Capacity = _originalCapacity;
        ExpressionParseCache.Clear();
    }

    [Fact]
    public async Task RepeatedEvaluation_HitsCache_AndDoesNotReparse()
    {
        ExpressionParseCache.Capacity = 64;
        const string expression = "1001 + 23 + 4 + 5 + 6";

        using var hits = new CounterDelta("tinkwell.expressions.parse_cache.hits");
        using var misses = new CounterDelta("tinkwell.expressions.parse_cache.misses");

        await _evaluator.EvaluateAsync(expression);
        await _evaluator.EvaluateAsync(expression);
        await _evaluator.EvaluateAsync(expression);

        Assert.Equal(2, hits.Value);
        Assert.Equal(1, misses.Value);
        Assert.Equal(1, ExpressionParseCache.Count);
    }

    [Fact]
    public async Task DistinctExpressions_ProduceDistinctEntries()
    {
        ExpressionParseCache.Capacity = 64;

        await _evaluator.EvaluateAsync("100 + 1");
        await _evaluator.EvaluateAsync("100 + 2");
        await _evaluator.EvaluateAsync("100 + 3");

        Assert.Equal(3, ExpressionParseCache.Count);
    }

    [Fact]
    public async Task Capacity_Bounded_EvictsLeastRecentlyUsed()
    {
        ExpressionParseCache.Capacity = 4;

        using var evictions = new CounterDelta("tinkwell.expressions.parse_cache.evictions");

        await _evaluator.EvaluateAsync("2001 + 1");
        await _evaluator.EvaluateAsync("2001 + 2");
        await _evaluator.EvaluateAsync("2001 + 3");
        await _evaluator.EvaluateAsync("2001 + 4");
        Assert.Equal(4, ExpressionParseCache.Count);

        await _evaluator.EvaluateAsync("2001 + 5");

        Assert.Equal(4, ExpressionParseCache.Count);
        Assert.Equal(1, evictions.Value);
    }

    [Fact]
    public async Task CapacityZero_DisablesCaching()
    {
        ExpressionParseCache.Capacity = 0;

        using var hits = new CounterDelta("tinkwell.expressions.parse_cache.hits");
        using var misses = new CounterDelta("tinkwell.expressions.parse_cache.misses");

        await _evaluator.EvaluateAsync("3001 + 1");
        await _evaluator.EvaluateAsync("3001 + 1");

        Assert.Equal(0, ExpressionParseCache.Count);
        Assert.Equal(0, hits.Value);
        Assert.Equal(2, misses.Value);
    }

    [Fact]
    public async Task ParseFailure_IsNotCached()
    {
        ExpressionParseCache.Capacity = 64;
        const string broken = "4001 +";

        await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => _evaluator.EvaluateAsync(broken));
        await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => _evaluator.EvaluateAsync(broken));

        Assert.Equal(0, ExpressionParseCache.Count);
    }

    [Fact]
    public void SettingCapacity_TrimsExistingEntries()
    {
        ExpressionParseCache.Capacity = 16;
        for (int i=0; i < 10; ++i)
            DependencyWalker<string>.ExtractParameters($"5001 + {i}");

        Assert.Equal(10, ExpressionParseCache.Count);

        ExpressionParseCache.Capacity = 3;

        Assert.Equal(3, ExpressionParseCache.Count);
    }

    [Fact]
    public void NegativeCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ExpressionParseCache.Capacity = -1);
    }

    [Fact]
    public async Task ConcurrentEvaluations_OfSameExpression_DoNotShareParameters()
    {
        ExpressionParseCache.Capacity = 64;
        const string expression = "[x] * 10 + 7";

        var tasks = Enumerable.Range(0, 64).Select(i => Task.Run(async () =>
        {
            var parameters = new Dictionary<string, object?> { ["x"] = i };
            var result = await _evaluator.EvaluateAsync(expression, parameters);
            Assert.Equal(i * 10 + 7, Convert.ToInt32(result));
        }));

        await Task.WhenAll(tasks);

        Assert.Equal(1, ExpressionParseCache.Count);
    }

    [Theory]
    [InlineData("9001 + 1", 9002)]
    [InlineData("9002 * 2", 18004)]
    [InlineData("9003 - 4", 8999)]
    public async Task ParityWithUncachedEvaluation(string expression, int expected)
    {
        ExpressionParseCache.Capacity = 0;
        var first = await _evaluator.EvaluateAsync(expression);

        ExpressionParseCache.Capacity = 64;
        var hot = await _evaluator.EvaluateAsync(expression);
        var hotAgain = await _evaluator.EvaluateAsync(expression);

        Assert.Equal(expected, Convert.ToInt32(first));
        Assert.Equal(expected, Convert.ToInt32(hot));
        Assert.Equal(expected, Convert.ToInt32(hotAgain));
    }

    [Fact]
    public void DependencyWalker_AlsoUsesCache()
    {
        ExpressionParseCache.Capacity = 64;
        const string expression = "[a] + [b] * 2 + 6001";

        using var hits = new CounterDelta("tinkwell.expressions.parse_cache.hits");
        using var misses = new CounterDelta("tinkwell.expressions.parse_cache.misses");

        var first = DependencyWalker<string>.ExtractParameters(expression);
        var second = DependencyWalker<string>.ExtractParameters(expression);

        Assert.Equal(first, second);
        Assert.Equal(1, misses.Value);
        Assert.Equal(1, hits.Value);
    }

    private sealed class CounterDelta : IDisposable
    {
        private readonly MeterListener _listener;
        private long _value;

        public CounterDelta(string instrumentName)
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == OtMetrics.MeterName
                        && instrument.Name == instrumentName)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
                Interlocked.Add(ref _value, measurement));
            _listener.Start();
        }

        public long Value => Interlocked.Read(ref _value);

        public void Dispose() => _listener.Dispose();
    }
}
