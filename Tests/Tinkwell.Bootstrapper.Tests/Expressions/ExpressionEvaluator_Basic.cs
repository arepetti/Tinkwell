using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Bootstrapper.Tests.Expressions;

public class ExpressionEvaluator_Basic
{
    private readonly ExpressionEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_SimpleExpression_ReturnsCorrectResult()
    {
        Assert.Equal(2, _evaluator.Evaluate("1 + 1", null));
    }

    [Fact]
    public void EvaluateString_SimpleExpression_ReturnsCorrectResult()
    {
        Assert.Equal("2", _evaluator.EvaluateString("1 + 1", null));
    }

    [Fact]
    public void EvaluateBool_SimpleExpression_ReturnsCorrectResult()
    {
        Assert.True(_evaluator.EvaluateBool("true and true", null));
        Assert.False(_evaluator.EvaluateBool("false or false", null));
    }

    [Fact]
    public void EvaluateBool_InvalidCast_ThrowsBootstrapperException()
    {
        var ex = Assert.Throws<BootstrapperException>(() => _evaluator.EvaluateBool("\"hello\"", null));
        Assert.Contains("Cannot convert", ex.Message);
    }

    [Fact]
    public void EvaluateDouble_ConvertsFromStrings()
    {
        Assert.Equal(1.0, _evaluator.EvaluateDoble("'1.0'", null));
    }

    [Fact]
    public void Evaluate_InvalidExpression_ThrowsBootstrapperException()
    {
        var ex = Assert.Throws<BootstrapperException>(() => _evaluator.Evaluate("1 + ", null));
        Assert.Contains("Error evaluating an expression", ex.Message);
    }

    [Fact]
    public void TryEvaluateDouble_ReturnsTrueForValidDoubles()
    {
        Assert.True(_evaluator.TryEvaluateDouble("1.0", null, out var value));
        Assert.Equal(1.0, value);
    }

    [Fact]
    public void TryEvaluateDouble_ReturnsFalseForInvalidDoubles()
    {
        Assert.False(_evaluator.TryEvaluateDouble("'a'", null, out var value));
    }

    [Fact]
    public void TryEvaluateDouble_ThrowsForInvalidExpressions()
    {
        Assert.Throws<BootstrapperException>(() =>
            _evaluator.TryEvaluateDouble("1 +", null, out var value));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("'true'", true)]
    [InlineData("1", true)]
    [InlineData("100", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    public void TryEvaluateBool_ReturnsTrueForValidBooleans(string input, bool expected)
    {
        Assert.True(_evaluator.TryEvaluateBool(input, null, out var value));
        Assert.Equal(value, expected);
    }
}
