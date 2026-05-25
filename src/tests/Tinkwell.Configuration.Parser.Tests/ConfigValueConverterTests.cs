using Tinkwell.Configuration;

namespace Tinkwell.Configuration.Parser.Tests;

/// <summary>
/// Exercises <see cref="ConfigValueConverter"/> conversion paths and edge cases.
/// </summary>
public class ConfigValueConverterTests
{
    private static readonly SourceLocation Loc = new("t.tw", 1, 1);

    [Fact]
    public void String_ToString_Succeeds()
    {
        var v = ConfigValueConverter.ConvertTo<string>(new StringValue("x"), Loc);
        Assert.Equal("x", v);
    }

    [Fact]
    public void String_ToBoolean_RecognizesSynonyms()
    {
        Assert.True(ConfigValueConverter.ConvertTo<bool>(new StringValue("yes"), Loc));
        Assert.False(ConfigValueConverter.ConvertTo<bool>(new StringValue("off"), Loc));
    }

    [Fact]
    public void String_ToInt_Throws()
    {
        Assert.Throws<ConfigurationConversionException>(
            () => ConfigValueConverter.ConvertTo<int>(new StringValue("42"), Loc));
    }

    [Fact]
    public void Long_ToInt_Exact_Succeeds()
    {
        Assert.Equal(42, ConfigValueConverter.ConvertTo<int>(new LongValue(42), Loc));
    }

    [Fact]
    public void Long_ToByte_Overflow_Throws()
    {
        Assert.Throws<ConfigurationConversionException>(
            () => ConfigValueConverter.ConvertTo<byte>(new LongValue(1024), Loc));
    }

    [Fact]
    public void Double_WithFraction_ToInt_Throws()
    {
        Assert.Throws<ConfigurationConversionException>(
            () => ConfigValueConverter.ConvertTo<int>(new DoubleValue(3.14), Loc));
    }

    [Fact]
    public void Double_Whole_ToInt_Succeeds()
    {
        Assert.Equal(3, ConfigValueConverter.ConvertTo<int>(new DoubleValue(3.0), Loc));
    }

    [Fact]
    public void Expression_WithoutEvaluator_UsesStringRules()
    {
        var v = ConfigValueConverter.ConvertTo<string>(new ExpressionValue("x + 1"), Loc);
        Assert.Equal("x + 1", v);
    }

    [Fact]
    public void Expression_WithEvaluator_InvokesCallback()
    {
        object Eval(string expr, Type t) => t == typeof(int) && expr == "1+2" ? 3 : 0;
        var v = ConfigValueConverter.ConvertTo<int>(
            new ExpressionValue("1+2"), Loc, Eval);
        Assert.Equal(3, v);
    }

    [Fact]
    public void Bool_ToInt_Throws()
    {
        Assert.Throws<ConfigurationConversionException>(
            () => ConfigValueConverter.ConvertTo<int>(BoolValue.True, Loc));
    }

    [Fact]
    public void NullableInt_Unwraps()
    {
        int? v = ConfigValueConverter.ConvertTo<int?>(new LongValue(7), Loc);
        Assert.Equal(7, v);
    }
}
