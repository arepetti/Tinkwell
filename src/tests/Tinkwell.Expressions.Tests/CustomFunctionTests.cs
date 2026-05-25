using System.Linq;
using NCalc.Handlers;
using Tinkwell.Expressions.Functions;

namespace Tinkwell.Expressions.Tests;

public class CustomFunctionTests
{
    // Custom handler sets Result = null; NCalc must still treat the call as satisfied (F12).
    [Fact]
    public async Task CustomFunction_ReturningNull_IsTreatedAsHandled()
    {
        var eval = new ExpressionEvaluator(new IExpressionFunction[] { new ReturnsNullFunction() });
        var result = await eval.EvaluateAsync("returns_null()");
        Assert.Null(result);
    }

    // --- Custom composition / nesting ---

    [Fact]
    public async Task NestedCustomAndBuiltin_ExpressionComposes()
    {
        var functions = ExpressionFunctionDiscovery.BuiltIn()
            .Append(new ReturnsTenFunction())
            .ToList();
        var eval = new ExpressionEvaluator(functions);
        var result = await eval.EvaluateAsync("cint(returns_ten())");
        Assert.Equal(10, Convert.ToInt32(result));
    }

    [Fact]
    public async Task CaseInsensitiveBuiltin_WithSnakeCaseCustom_BothDispatch()
    {
        var eval = new ExpressionEvaluator(
            ExpressionFunctionDiscovery.BuiltIn().Append(new ReturnsStringHi()).ToList());
        const string expr = "if(1 = 1, concat(cstr(abs(-5)), to_lower(returns_hi())), '')";
        var asString = await eval.EvaluateStringAsync(expr);
        Assert.Equal("5hi", asString, StringComparer.Ordinal);
    }

    private sealed class ReturnsStringHi : IExpressionFunction
    {
        public string Name => "returns_hi";

        public object? Invoke(FunctionArgs args) => "HI";
    }

    private sealed class ReturnsTenFunction : IExpressionFunction
    {
        public string Name => "returns_ten";

        public object? Invoke(FunctionArgs args) => 10.0;
    }

    private sealed class ReturnsNullFunction : IExpressionFunction
    {
        public string Name => "returns_null";

        public object? Invoke(FunctionArgs args) => null;
    }
}
