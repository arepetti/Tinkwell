using System.Globalization;
using System.Text.Json.Serialization;

namespace Tinkwell.Configuration;

/// <summary>
/// Abstract base type for all configuration values.
/// Each derived type represents a distinct literal or expression
/// that can appear on the right-hand side of a property assignment
/// or as a modifier argument.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="System.Text.Json.Serialization.JsonDerivedTypeAttribute"/> attributes enable polymorphic
/// round-trip serialization with <c>System.Text.Json</c> (type discriminator
/// in a <c>$type</c> property).
/// </para>
/// <para>
/// The recognized value types are:
/// <list type="bullet">
///   <item><see cref="StringValue"/> — a double-quoted string (<c>"hello"</c>) or an unquoted identifier.</item>
///   <item><see cref="ExpressionValue"/> — a verbatim expression string (<c>@"expr"</c>) or parenthesized expression (<c>(expr)</c>).</item>
///   <item><see cref="LongValue"/> — a 64-bit integer (<c>42</c>, <c>-10</c>).</item>
///   <item><see cref="DoubleValue"/> — a 64-bit floating-point number (<c>3.14</c>).</item>
///   <item><see cref="BoolValue"/> — <c>true</c> or <c>false</c>.</item>
/// </list>
/// A grammar-only type, <c>InterpolatedStringValue</c> (<c>$"..."</c> in source), is defined
/// in <c>TwGrammar.cs</c>; the preprocessor rewrites it to <see cref="StringValue"/>.
/// </para>
/// </remarks>
[JsonDerivedType(typeof(StringValue), "string")]
[JsonDerivedType(typeof(ExpressionValue), "expression")]
[JsonDerivedType(typeof(LongValue), "long")]
[JsonDerivedType(typeof(DoubleValue), "double")]
[JsonDerivedType(typeof(BoolValue), "bool")]
public abstract record ConfigValue;

/// <summary>
/// A plain string value, produced from a double-quoted literal (<c>"hello world"</c>),
/// an unquoted identifier (<c>simple</c>), or the rendering of an interpolated string (<c>$"..."</c>).
/// </summary>
/// <param name="Value">The string content after escape processing.</param>
public sealed record StringValue(string Value) : ConfigValue
{
    /// <inheritdoc/>
    public override string ToString() => $"\"{Value}\"";
}

/// <summary>
/// An opaque expression value, produced from a verbatim expression string
/// (<c>@"expr"</c>) or a parenthesized expression (<c>(expr)</c>).
/// The parser does not evaluate expressions — they are passed through
/// for derived classes or callers to interpret.
/// </summary>
/// <param name="Expression">The raw expression text (without delimiters).</param>
public sealed record ExpressionValue(string Expression) : ConfigValue
{
    /// <inheritdoc/>
    public override string ToString() => $"@\"{Expression}\"";
}

/// <summary>
/// A 64-bit signed integer value (e.g. <c>42</c>, <c>-10</c>, <c>50051</c>).
/// </summary>
/// <param name="Value">The integer value.</param>
public sealed record LongValue(long Value) : ConfigValue
{
    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}

/// <summary>
/// A 64-bit floating-point value (e.g. <c>3.14</c>, <c>-0.5</c>).
/// </summary>
/// <param name="Value">The floating-point value.</param>
public sealed record DoubleValue(double Value) : ConfigValue
{
    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// A boolean value (<c>true</c> or <c>false</c>).
/// </summary>
/// <param name="Value">The boolean value.</param>
public sealed record BoolValue(bool Value) : ConfigValue
{
    /// <summary>Singleton for <c>true</c>.</summary>
    public static readonly BoolValue True = new(true);

    /// <summary>Singleton for <c>false</c>.</summary>
    public static readonly BoolValue False = new(false);

    /// <inheritdoc/>
    public override string ToString() => Value ? "true" : "false";
}
