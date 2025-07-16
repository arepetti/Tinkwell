using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Bootstrapper.Tests.Expressions;

public class ExpressionEvaluator_Conversions
{
    private readonly ExpressionEvaluator _evaluator = new();

    [Fact]
    public void CInt_ConvertsToInteger()
    {
        Assert.Equal(123, _evaluator.Evaluate("cint('123')", null));
    }

    [Fact]
    public void CLong_ConvertsToLong()
    {
        Assert.Equal(123L, _evaluator.Evaluate("clong('123')", null));
    }

    [Fact]
    public void CFloat_ConvertsToFloat()
    {
        Assert.Equal(123.45f, _evaluator.Evaluate("cfloat('123.45')", null));
    }

    [Fact]
    public void CDouble_ConvertsToDouble()
    {
        Assert.Equal(123.45, _evaluator.Evaluate("cdouble('123.45')", null));
    }

    [Fact]
    public void CStr_ConvertsToString()
    {
        Assert.Equal("123", _evaluator.Evaluate("cstr(123)", null));
    }
}
