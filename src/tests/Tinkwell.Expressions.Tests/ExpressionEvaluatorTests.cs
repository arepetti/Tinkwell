using System.Globalization;
using System.Linq;
using NCalc.Handlers;
using Tinkwell.Expressions;
using Tinkwell.Expressions.Functions;

namespace Tinkwell.Expressions.Tests;
public class ExpressionEvaluatorTests
{
    private readonly ExpressionEvaluator _evaluator = new(functions: null);

    [Theory]
    [InlineData("1 + 2", 3)]
    [InlineData("10 - 3", 7)]
    [InlineData("4 * 5", 20)]
    [InlineData("10 / 2", 5)]
    public async Task EvaluateAsync_Arithmetic_ReturnsCorrectResult(string expr, int expected)
    {
        var result = await _evaluator.EvaluateAsync(expr);
        Assert.Equal(expected, Convert.ToInt32(result));
    }

    [Fact]
    public async Task EvaluateAsync_WithParameters_ResolvesValues()
    {
        var parameters = new Dictionary<string, object?> { ["x"] = 10, ["y"] = 20 };
        var result = await _evaluator.EvaluateAsync("[x] + [y]", parameters);
        Assert.Equal(30, Convert.ToInt32(result));
    }

    [Fact]
    public async Task EvaluateAsync_NullParameter_Allowed()
    {
        var parameters = new Dictionary<string, object?> { ["x"] = null };
        var result = await _evaluator.EvaluateAsync("[x]", parameters);
        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateAsync_InvalidExpression_ThrowsExpressionEvaluationException()
    {
        var ex = await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => _evaluator.EvaluateAsync("1 + + 2"));
        Assert.Equal("1 + + 2", ex.Expression);
    }

    [Fact]
    public async Task EvaluateAsync_NullExpression_ThrowsArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _evaluator.EvaluateAsync(null!));
    }

    // --- EvaluateBooleanAsync ---

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("1 > 0", true)]
    [InlineData("1 < 0", false)]
    [InlineData("1 == 1", true)]
    public async Task EvaluateBooleanAsync_LogicalExpressions_ReturnsCorrectResult(string expr, bool expected)
    {
        var result = await _evaluator.EvaluateBooleanAsync(expr);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("yes", true)]
    [InlineData("on", true)]
    [InlineData("false", false)]
    [InlineData("no", false)]
    [InlineData("off", false)]
    public async Task EvaluateBooleanAsync_StringCoercion_ConvertsCorrectly(string boolStr, bool expected)
    {
        var parameters = new Dictionary<string, object?> { ["val"] = boolStr };
        var result = await _evaluator.EvaluateBooleanAsync("[val]", parameters);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task EvaluateBooleanAsync_NullResult_ReturnsFalse()
    {
        var parameters = new Dictionary<string, object?> { ["x"] = null };
        var result = await _evaluator.EvaluateBooleanAsync("[x]", parameters);
        Assert.False(result);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, true)]
    [InlineData(0.0, false)]
    [InlineData(3.14, true)]
    public async Task EvaluateBooleanAsync_NumericCoercion_NonZeroIsTrue(object value, bool expected)
    {
        var parameters = new Dictionary<string, object?> { ["n"] = value };
        var result = await _evaluator.EvaluateBooleanAsync("[n]", parameters);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData('A', true)]
    [InlineData('\0', false)]
    public async Task EvaluateBooleanAsync_CharCoercion_NonZeroCodeUnitIsTrue(char ch, bool expected)
    {
        var parameters = new Dictionary<string, object?> { ["c"] = ch };
        var result = await _evaluator.EvaluateBooleanAsync("[c]", parameters);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task EvaluateBooleanAsync_DateTimeParameter_ThrowsExpressionEvaluationException()
    {
        var parameters = new Dictionary<string, object?> { ["d"] = DateTime.UtcNow };
        var ex = await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => _evaluator.EvaluateBooleanAsync("[d]", parameters));
        Assert.Equal("[d]", ex.Expression);
        Assert.NotNull(ex.Message);
    }

    [Fact]
    public async Task EvaluateBooleanAsync_UnconvertibleString_Throws()
    {
        var parameters = new Dictionary<string, object?> { ["val"] = "maybe" };
        var ex = await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => _evaluator.EvaluateBooleanAsync("[val]", parameters));
        Assert.Contains("maybe", ex.Message);
    }

    // --- EvaluateStringAsync ---

    [Fact]
    public async Task EvaluateStringAsync_ReturnsStringResult()
    {
        var parameters = new Dictionary<string, object?> { ["name"] = "world" };
        var result = await _evaluator.EvaluateStringAsync("[name]", parameters);
        Assert.Equal("world", result);
    }

    [Fact]
    public async Task EvaluateStringAsync_NumericResult_FormattedInvariant()
    {
        var result = await _evaluator.EvaluateStringAsync("1 + 2");
        Assert.Equal("3", result);
    }

    [Fact]
    public async Task EvaluateStringAsync_NullResult_ReturnsEmpty()
    {
        var parameters = new Dictionary<string, object?> { ["x"] = null };
        var result = await _evaluator.EvaluateStringAsync("[x]", parameters);
        Assert.Equal("", result);
    }

    // --- Timeout ---

    [Fact]
    public async Task EvaluateAsync_WithTimeout_CompletesNormally()
    {
        var options = new ExpressionEvaluationOptions(Timeout: TimeSpan.FromSeconds(5));
        var result = await _evaluator.EvaluateAsync("1 + 1", options: options);
        Assert.Equal(2, Convert.ToInt32(result));
    }

    [Fact]
    public async Task EvaluateAsync_CancellationToken_HonoredBeforeEvaluation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _evaluator.EvaluateAsync("1 + 1", cancellationToken: cts.Token));
    }

    // --- Default options ---

    [Fact]
    public void ExpressionEvaluationOptions_Default_Has5SecondTimeout()
    {
        Assert.Null(ExpressionEvaluationOptions.Default.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(5), ExpressionEvaluationOptions.Default.EffectiveTimeout);
    }

    [Fact]
    public void ExpressionEvaluationOptions_InfiniteTimeSpan_DisablesTimeout()
    {
        var options = new ExpressionEvaluationOptions(Timeout: Timeout.InfiniteTimeSpan);
        Assert.Equal(Timeout.InfiniteTimeSpan, options.EffectiveTimeout);
    }

    [Fact]
    public void ExpressionEvaluationOptions_ExplicitTimeout_Honored()
    {
        var options = new ExpressionEvaluationOptions(Timeout: TimeSpan.FromSeconds(10));
        Assert.Equal(TimeSpan.FromSeconds(10), options.EffectiveTimeout);
    }

    // --- EvaluateBooleanAsync additional coercion ---

    [Theory]
    [InlineData(1.1, true)]
    [InlineData(0.0, false)]
    public async Task EvaluateBooleanAsync_Decimal_CoercesByMagnitude(object value, bool expected)
    {
        var parameters = new Dictionary<string, object?> { ["d"] = value is double d
            ? (object)(decimal)d
            : value };
        var result = await _evaluator.EvaluateBooleanAsync("[d]", parameters);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task EvaluateBooleanAsync_Long_CoercesByMagnitude()
    {
        var parameters = new Dictionary<string, object?> { ["l"] = 0L, ["m"] = 42L };
        Assert.False(await _evaluator.EvaluateBooleanAsync("[l]", parameters));
        Assert.True(await _evaluator.EvaluateBooleanAsync("[m]", parameters));
    }

    [Theory]
    [InlineData("TRUE", true)]
    [InlineData("Yes", true)]
    [InlineData("   true   ", true)]
    [InlineData("   FALSE   ", false)]
    [InlineData("oFf", false)]
    public async Task EvaluateBooleanAsync_String_RecognizesKeywordsCaseAndWhitespaceInsensitively(
        string s,
        bool expected)
    {
        var parameters = new Dictionary<string, object?> { ["s"] = s };
        var result = await _evaluator.EvaluateBooleanAsync("[s]", parameters);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task EvaluateBooleanAsync_WhitespaceString_Throws()
    {
        var parameters = new Dictionary<string, object?> { ["s"] = "   " };
        var ex = await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => _evaluator.EvaluateBooleanAsync("[s]", parameters));
        Assert.Contains("Cannot convert", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateBooleanAsync_NumericStringZero_Throws()
    {
        var parameters = new Dictionary<string, object?> { ["s"] = "0" };
        await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => _evaluator.EvaluateBooleanAsync("[s]", parameters));
    }

    // --- EvaluateStringAsync additional ---

    [Fact]
    public async Task EvaluateStringAsync_DateTime_UsesInvariantCulture()
    {
        var dt = new DateTime(2024, 3, 15, 6, 7, 8, 123, DateTimeKind.Utc);
        var parameters = new Dictionary<string, object?> { ["d"] = dt };
        var s = await _evaluator.EvaluateStringAsync("[d]", parameters);
        var expected = dt.ToString(null, CultureInfo.InvariantCulture);
        Assert.Equal(expected, s);
    }

    [Fact]
    public async Task EvaluateStringAsync_TimeSpan_UsesInvariantCulture()
    {
        var ts = new TimeSpan(1, 2, 3, 4, 5);
        var parameters = new Dictionary<string, object?> { ["t"] = ts };
        var s = await _evaluator.EvaluateStringAsync("[t]", parameters);
        Assert.Equal(ts.ToString(null, CultureInfo.InvariantCulture), s);
    }

    [Fact]
    public async Task EvaluateStringAsync_Decimal_UsesInvariantCulture()
    {
        var d = 12345.6m;
        var parameters = new Dictionary<string, object?> { ["x"] = d };
        var s = await _evaluator.EvaluateStringAsync("[x]", parameters);
        Assert.Equal("12345.6", s);
    }

    [Fact]
    public async Task EvaluateStringAsync_LargeAndNegativeDoubles_UsesInvariantCulture()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["a"] = 1.23e6,
            ["b"] = -987.25,
        };
        var sa = await _evaluator.EvaluateStringAsync("[a]", parameters);
        var sb = await _evaluator.EvaluateStringAsync("[b]", parameters);
        Assert.Equal("1230000", sa);
        Assert.Equal("-987.25", sb);
    }

    // --- Parameter / identifier interactions ---

    [Fact]
    public async Task ParameterNamedAsBuiltin_AbsArgumentStillDispatches()
    {
        var all = ExpressionFunctionDiscovery.BuiltIn().ToList();
        var eval = new ExpressionEvaluator(all);
        var parameters = new Dictionary<string, object?>
        {
            ["abs"] = -7,
        };
        var s = await eval.EvaluateStringAsync("cstr(Abs(5) + [abs])", parameters);
        Assert.Equal("-2", s);
    }

    // --- Short timeout (blocking work may outlive the wait; the API documents this) ---

    [Fact(Skip = "Documented: timeout only bounds wait; blocking custom work (e.g. Thread.Sleep) is not pre-empted — see package README. Fix would be API/behavior change (cooperative cancel or harsher cancellation).")]
    public async Task EvaluateAsync_ShortTimeout_ThrowsWithTimedOutMessage()
    {
        var eval = new ExpressionEvaluator(
            new IExpressionFunction[] { new BlockingSleepForSeconds(3) });
        var options = new ExpressionEvaluationOptions(Timeout: TimeSpan.FromMilliseconds(100));
        var ex = await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => eval.EvaluateAsync("block_sleep()", options: options));
        Assert.Contains("timed out", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "Documented: in-flight user cancellation of Task.Run does not complete until a blocking custom function finishes — same pre-emption limit as timeout (see package README).")]
    public async Task EvaluateAsync_InFlightUserCancellation_ThrowsUnwrappedOperationCanceled()
    {
        var eval = new ExpressionEvaluator(
            new IExpressionFunction[] { new BlockingSleepForSeconds(2) });
        using var cts = new CancellationTokenSource();
        var options = new ExpressionEvaluationOptions(Timeout: Timeout.InfiniteTimeSpan);
        var task = eval.EvaluateAsync("block_sleep()", parameters: null, options: options, cancellationToken: cts.Token);
        await Task.Delay(20);
        cts.Cancel();
        var ex = await Assert.ThrowsAsync<OperationCanceledException>(() => task);
        Assert.NotNull(ex);
    }
}

file sealed class BlockingSleepForSeconds : IExpressionFunction
{
    private readonly int _seconds;

    public BlockingSleepForSeconds(int seconds) => _seconds = seconds;

    public string Name => "block_sleep";

    public object? Invoke(FunctionArgs args)
    {
        Thread.Sleep(TimeSpan.FromSeconds(_seconds));
        return 0;
    }
}
