using System.Globalization;

namespace Tinkwell.Expressions.Functions.Builtins;

/// <summary>
/// <c>cint(x)</c> — Coerces the argument to <see cref="int"/> (invariant <c>ChangeType</c>). <c>ExpressionFunction</c> names this <c>cint</c> (class <c>CInt</c> has no inserted underscore; see package README).
/// </summary>
sealed class CInt : UnaryFunction<int>
{
    protected override object? Call(int arg) => arg;
}

/// <summary>
/// <c>clong(x)</c> — Coerces the argument to <see cref="long"/> (invariant <c>ChangeType</c>).
/// </summary>
sealed class CLong : UnaryFunction<long>
{
    protected override object? Call(long arg) => arg;
}

/// <summary>
/// <c>cfloat(x)</c> — Coerces the argument to <see cref="float"/> (invariant <c>ChangeType</c>).
/// </summary>
sealed class CFloat : UnaryFunction<float>
{
    protected override object? Call(float arg) => arg;
}

/// <summary>
/// <c>cdouble(x)</c> — Coerces the argument to <see cref="double"/> (invariant <c>ChangeType</c>).
/// </summary>
sealed class CDouble : UnaryFunction<double>
{
    protected override object? Call(double arg) => arg;
}

/// <summary>
/// <c>cstr(x)</c> — String as-is; other types are converted to string with invariant rules where applicable.
/// </summary>
sealed class CStr : UnaryFunction<string>
{
    protected override object? Call(string arg) => arg;
}

/// <summary>
/// <c>cbool(x)</c> — Boolean coercion with Tinkwell rules (null false; string tokens; numeric non-zero; legacy "other reference type" = true). See package README.
/// </summary>
sealed class CBool : UnaryFunction<object?>
{
    protected override object? Call(object? arg) => arg switch
    {
        null => false,
        bool b => b,
        string s => s.Trim().ToLowerInvariant() is "true" or "yes" or "on",
        char c => c != '\0',
        sbyte or byte or short or ushort or int or uint or long or ulong or nint or nuint
        or float or double or decimal => CBoolToDouble(arg) != 0.0,
        _ => true
    };

    private static double CBoolToDouble(object value)
    {
        try
        {
            return ((IConvertible)value).ToDouble(CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new ArgumentException($"cbool() could not treat the value as a numeric test: {ex.Message}", ex);
        }
    }
}
