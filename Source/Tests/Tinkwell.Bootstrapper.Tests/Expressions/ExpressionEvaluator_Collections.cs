using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Bootstrapper.Tests.Expressions;

public class ExpressionEvaluator_Collections
{
    private readonly ExpressionEvaluator _evaluator = new();

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        Assert.Equal(3, _evaluator.Evaluate("count(items)", new { items = new List<int> { 1, 2, 3 } }));
        Assert.Equal(0, _evaluator.Evaluate("count(items)", new { items = new List<int>() }));
    }

    [Fact]
    public void At_ReturnsCorrectElement()
    {
        var args = new { items = new List<int> { 1, 2, 3 } };
        Assert.Equal(2, _evaluator.Evaluate("at(items, 1)", args));
        Assert.Throws<InvalidOperationException>(
            () => _evaluator.Evaluate("at(items, 5)", args));
    }

    [Fact]
    public void Skip_ReturnsSkippedElements()
    {
        var result = _evaluator.Evaluate("skip(items, 2)", new { items = new List<int> { 1, 2, 3, 4 } }) as System.Collections.IEnumerable;
        Assert.NotNull(result);
        Assert.Collection(result.Cast<int>(), item => Assert.Equal(3, item), item => Assert.Equal(4, item));
    }

    [Fact]
    public void Take_ReturnsTakenElements()
    {
        var result = _evaluator.Evaluate("take(items, 2)", new { items = new List<int> { 1, 2, 3, 4 } }) as System.Collections.IEnumerable;
        Assert.NotNull(result);
        Assert.Collection(result.Cast<int>(), item => Assert.Equal(1, item), item => Assert.Equal(2, item));
    }

    [Fact]
    public void First_ReturnsFirstElement()
    {
        Assert.Equal(1, _evaluator.Evaluate("first(items, 0)", new { items = new List<int> { 1, 2, 3 } }));
    }

    [Fact]
    public void Sum_ReturnsCorrectSum()
    {
        Assert.Equal(6.0, _evaluator.Evaluate("sum(items)", new { items = new List<int> { 1, 2, 3 } }));
    }

    [Fact]
    public void Avg_ReturnsCorrectAverage()
    {
        Assert.Equal(2.0, _evaluator.Evaluate("avg(items)", new { items = new List<int> { 1, 2, 3 } }));

        // Note: null and not zero because we have no elements
        Assert.Null(_evaluator.Evaluate("avg(null_items)", new { null_items = (List<int>?)null }));
    }

    [Fact]
    public void Any_ReturnsTrueIfAnyMatch()
    {
        var parameters = new { items = new List<object> { 1, 2, 3 } };
        Assert.True(_evaluator.EvaluateBool("any(items, 'value > 2')", parameters));
        Assert.False(_evaluator.EvaluateBool("any(items, 'value > 5')", parameters));
    }

    [Fact]
    public void All_ReturnsTrueIfAllMatch()
    {
        var parameters = new { items = new List<object> { 1, 2, 3 } };
        Assert.True(_evaluator.EvaluateBool("all(items, 'value < 5')", parameters));
        Assert.False(_evaluator.EvaluateBool("all(items, 'value > 2')", parameters));
    }
}
