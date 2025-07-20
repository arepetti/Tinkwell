using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Bootstrapper.Tests.Expressions;

public class ExpressionEvaluator_Strings
{
    private readonly ExpressionEvaluator _evaluator = new();

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" ", false)]
    [InlineData("abc", false)]
    public void IsNullOrEmpty_ReturnsCorrectResult(string? value, bool expected)
    {
        Assert.Equal(expected, _evaluator.Evaluate($"is_null_or_empty(value)", new { value }));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" ", true)]
    [InlineData("abc", false)]
    public void IsNullOrWhiteSpace_ReturnsCorrectResult(string? value, bool expected)
    {
        Assert.Equal(expected, _evaluator.Evaluate($"is_null_or_white_space(value)", new { value }));
    }

    [Theory]
    [InlineData("hello", 5)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void Length_ReturnsCorrectLength(string? value, int expected)
    {
        Assert.Equal(expected, _evaluator.Evaluate($"length(value)", new { value }));
    }

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void OrEmpty_ReturnsCorrectString(string? value, string expected)
    {
        Assert.Equal(expected, _evaluator.Evaluate($"or_empty(value)", new { value }));
    }

    [Theory]
    [InlineData("  hello  ", "hello")]
    [InlineData("", "")]
    public void Trim_ReturnsTrimmedString(string value, string expected)
    {
        Assert.Equal(expected, _evaluator.Evaluate($"trim(value)", new { value }));
    }

    [Fact]
    public void Concat_ReturnsConcatenatedString()
    {
        Assert.Equal("HelloWorld", _evaluator.Evaluate("concat('Hello', 'World')", null));
        Assert.Equal("Hello", _evaluator.Evaluate("concat('Hello', null_param)", new { null_param = (object?)null }));
        Assert.Equal("World", _evaluator.Evaluate("concat(null_param, 'World')", new { null_param = (object?)null }));
    }

    [Fact]
    public void Split_ReturnsCorrectArray()
    {
        var result = _evaluator.Evaluate("split('a,b,c', ',')", null) as System.Collections.IEnumerable;
        Assert.NotNull(result);
        Assert.Collection(result.Cast<string>(), item => Assert.Equal("a", item), item => Assert.Equal("b", item), item => Assert.Equal("c", item));
    }

    [Fact]
    public void SegmentAt_ReturnsCorrectSegment()
    {
        Assert.Equal("b", _evaluator.Evaluate("segment_at('a,b,c', ',', 1)", null));
    }

    [Fact]
    public void Join_ReturnsJoinedString()
    {
        var parameters = new { items = new List<string> { "a", "b", "c" } };
        Assert.Equal("a-b-c", _evaluator.Evaluate("join('-', items)", parameters));
    }

    [Fact]
    public void ToLower_ReturnsLowercaseString()
    {
        Assert.Equal("hello", _evaluator.Evaluate("to_lower('Hello')", null));
    }

    [Fact]
    public void ToUpper_ReturnsUppercaseString()
    {
        Assert.Equal("HELLO", _evaluator.Evaluate("to_upper('Hello')", null));
    }

    [Fact]
    public void RegexMatch_ReturnsCorrectResult()
    {
        Assert.True(_evaluator.EvaluateBool("regex_match('hello world', 'hello')", null));
        Assert.False(_evaluator.EvaluateBool("regex_match('hello world', 'xyz')", null));
    }

    [Theory]
    [InlineData("match('blue', 'blue')", true)]
    [InlineData("match('blue', 'green')", false)]
    [InlineData("match('blue', 'blu?')", true)]
    [InlineData("match('blue', 'bl*')", true)]
    public void Match_MatchesWildcards(string input, bool result)
    {
        // No need for extensive testing, they're covered in the
        // tests for TextHelpers.
        Assert.Equal(result, _evaluator.EvaluateBool(input, null));
    }

    [Fact]
    public void JsonEncode_EncodesCorrectly()
    {
        Assert.Equal("\"hello\"", _evaluator.Evaluate("json_encode('hello')", null));
    }

    [Fact]
    public void UrlEncode_EncodesCorrectly()
    {
        Assert.Equal("Hello+World", _evaluator.Evaluate("url_encode('Hello World')", null));
    }
}
