using System.Globalization;
using System.Linq;
using Tinkwell.Expressions.Functions;

namespace Tinkwell.Expressions.Tests;

public class ExpressionDiscoveryAndBaseClassTests
{
    // --- FromAssembly / cache ---

    [Fact]
    public void FromAssembly_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ExpressionFunctionDiscovery.FromAssembly(null!));
    }

    [Fact]
    public void FromAssembly_OfThisAssembly_ListsAlphaBeforeZebra_Sorted()
    {
        var list = ExpressionFunctionDiscovery.FromAssembly(typeof(AlphaScanFunction).Assembly);
        var names = list.Select(f => f.Name).ToList();
        var idxAlpha = names.IndexOf("alpha");
        var idxZebra = names.IndexOf("zebra");
        Assert.True(idxAlpha >= 0 && idxZebra >= 0);
        Assert.True(idxAlpha < idxZebra, "alpha should sort before zebra; order=" + string.Join(", ", names));
    }

    [Fact]
    public void FromAssembly_SameAssemblyTwice_SameListInstance()
    {
        var asm = typeof(AlphaScanFunction).Assembly;
        var a = ExpressionFunctionDiscovery.FromAssembly(asm);
        var b = ExpressionFunctionDiscovery.FromAssembly(asm);
        Assert.Same(a, b);
    }

    // --- ExpressionFunction / arity (via built-in reflection) ---

    [Fact]
    public async Task Now_WrongArgCount_ThrowsWithClearMessage()
    {
        var eval = new ExpressionEvaluator(ExpressionFunctionDiscovery.BuiltIn());
        var ex = await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => eval.EvaluateAsync("now(1)"));
        Assert.Contains("no arguments", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cint_NullParameter_ThrowsChangeTypeError()
    {
        var eval = new ExpressionEvaluator(ExpressionFunctionDiscovery.BuiltIn());
        var parameters = new Dictionary<string, object?> { ["x"] = null };
        var ex = await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => eval.EvaluateAsync("cint([x])", parameters));
        Assert.Contains("cint", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(2.7, 3)]
    [InlineData(-1.9, -2)]
    public async Task Cint_Double_ConvertsWithStandardRounding(double d, int expected)
    {
        var eval = new ExpressionEvaluator(ExpressionFunctionDiscovery.BuiltIn());
        var r = await eval.EvaluateAsync($"cint({d.ToString(CultureInfo.InvariantCulture)})");
        Assert.Equal(expected, Convert.ToInt32(r));
    }

    [Fact]
    public async Task Cint_ValueOutsideInt32_RaisesFromChangeType()
    {
        var eval = new ExpressionEvaluator(ExpressionFunctionDiscovery.BuiltIn());
        var ex = await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => eval.EvaluateAsync("cint(2147483648)"));
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public async Task Cdouble_StringInvariant()
    {
        var eval = new ExpressionEvaluator(ExpressionFunctionDiscovery.BuiltIn());
        var r = await eval.EvaluateAsync("cdouble('3.14')");
        Assert.Equal(3.14, Convert.ToDouble(r, CultureInfo.InvariantCulture), 5);
    }

    [Fact]
    public async Task Cstr_NullYieldsNull()
    {
        var eval = new ExpressionEvaluator(ExpressionFunctionDiscovery.BuiltIn());
        var parameters = new Dictionary<string, object?> { ["x"] = null };
        var r = await eval.EvaluateAsync("cstr([x])", parameters);
        Assert.Null(r);
    }

    [Theory]
    [InlineData(1.0, true)]
    [InlineData(null, false)]
    public async Task Cbool_DoubleOrNull(object? p, bool expected)
    {
        var eval = new ExpressionEvaluator(ExpressionFunctionDiscovery.BuiltIn());
        var parameters = new Dictionary<string, object?> { ["p"] = p };
        var r = await eval.EvaluateBooleanAsync("cbool([p])", parameters);
        Assert.Equal(expected, r);
    }

    [Fact]
    public async Task Cbool_StringYes_CaseIgnored()
    {
        var eval = new ExpressionEvaluator(ExpressionFunctionDiscovery.BuiltIn());
        var r = await eval.EvaluateBooleanAsync("cbool('YeS')");
        Assert.True(r);
    }

    [Fact]
    public async Task DateAdd_PlusParseTimespanIsoDuration_Adds()
    {
        // ChangeType<TimeSpan> does not accept a raw string; compose with parse_timespan.
        var eval = new ExpressionEvaluator(ExpressionFunctionDiscovery.BuiltIn());
        const string expr =
            "date_add(parse_date('2020-01-01T00:00:00Z'), parse_timespan('1.00:00:00'))";
        var r = await eval.EvaluateAsync(expr);
        var dt = Assert.IsType<DateTime>(r);
        var br = await eval.EvaluateAsync("parse_date('2020-01-01T00:00:00Z')");
        var baseUtc = Assert.IsType<DateTime>(br);
        var expected = baseUtc + TimeSpan.Parse("1.00:00:00", CultureInfo.InvariantCulture);
        Assert.Equal(expected, dt, TimeSpan.FromSeconds(0.5));
    }
}
