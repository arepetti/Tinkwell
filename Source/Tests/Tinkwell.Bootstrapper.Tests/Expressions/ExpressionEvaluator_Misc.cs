using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Bootstrapper.Tests.Expressions;

public class ExpressionEvaluator_Misc
{
    private readonly ExpressionEvaluator _evaluator = new();

    [Theory]
    [InlineData(null, true)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    public void IsNull_ReturnsCorrectResult(object? value, bool expected)
    {
        Assert.Equal(expected, _evaluator.Evaluate($"is_null(value)", new { value }));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("abc", true)]
    [InlineData(123, true)]
    public void HasValue_ReturnsCorrectResult(object? value, bool expected)
    {
        Assert.Equal(expected, _evaluator.Evaluate($"has_value(value)", new { value }));
    }

    [Fact]
    public void Base64Encode_EncodesCorrectly()
    {
        Assert.Equal("SGVsbG8gV29ybGQ=", _evaluator.Evaluate("base64_encode('Hello World')", null));
    }

    [Fact]
    public void Base64Decode_DecodesCorrectly()
    {
        Assert.Equal("Hello World", _evaluator.Evaluate("base64_decode('SGVsbG8gV29ybGQ=')", null));
    }

    [Fact]
    public void Md5_ReturnsCorrectHash()
    {
        Assert.Equal("b10a8db164e0754105b7a99be72e3fe5", _evaluator.Evaluate("md5('Hello World')", null));
    }

    [Fact]
    public void Sha256_ReturnsCorrectHash()
    {
        Assert.Equal("a591a6d40bf420404a011733cfb7b190d62c65bf0bcda32b57b277d9ad9f146e", _evaluator.Evaluate("sha256('Hello World')", null));
    }

}
