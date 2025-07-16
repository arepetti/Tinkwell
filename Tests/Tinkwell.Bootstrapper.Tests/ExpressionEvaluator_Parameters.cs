using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Bootstrapper.Tests.Expressions;

public class ExpressionEvaluator_Parameters
{
    private readonly ExpressionEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_ParametersFromObject_ReturnsCorrectResult()
    {
        var parameters = new { a = 10, b = 20 };
        Assert.Equal(30, _evaluator.Evaluate("a + b", parameters));
    }

    [Fact]
    public void Evaluate_ParametersFromDictionary_ReturnsCorrectResult()
    {
        var parameters = new Dictionary<string, object> { { "a", 10 }, { "b", 20 } };
        Assert.Equal(30, _evaluator.Evaluate("a + b", parameters));
    }

    [Fact]
    public void Evaluate_NestedParametersFromObject_ReturnsCorrectResult()
    {
        var parameters = new { data = new { value = 100 } };
        Assert.Equal(100, _evaluator.Evaluate("[data.value]", parameters));
    }

    [Fact]
    public void Evaluate_NestedParametersFromDictionaryNotDotNotation_ReturnsCorrectResult()
    {
        var parameters = new Dictionary<string, object>
        {
            { "data", new Dictionary<string, object> { { "value", 200 } } }
        };

        // This must throw because we do not "flatten" parameters when they're dictionaries.
        // We do it in CLI tw templates then MAYBE we could move it here, if there is ever
        // the need to support it.
        Assert.Throws<BootstrapperException>(() => _evaluator.Evaluate("[data.value]", parameters));
    }

    [Fact]
    public void Evaluate_MixedParameters_ReturnsCorrectResult()
    {
        var parameters = new
        {
            a = 5,
            b = new Dictionary<string, object> { { "c", 10 } }
        };
        Assert.Equal(15, _evaluator.Evaluate("a + [b.c]", parameters));
    }
}
