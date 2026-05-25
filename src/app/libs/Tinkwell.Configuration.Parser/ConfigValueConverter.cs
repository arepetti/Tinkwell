using System.Globalization;
using System.Text;
using Tinkwell.Configuration;

namespace Tinkwell.Configuration.Parser;

/// <summary>
/// Converts <see cref="ConfigValue"/> instances to CLR types.
/// </summary>
/// <remarks>
/// <para>Conversion rules by source type:</para>
/// <list type="table">
///   <listheader>
///     <term>Source</term>
///     <description>Allowed targets</description>
///   </listheader>
///   <item>
///     <term><see cref="StringValue"/></term>
///     <description>
///       <see cref="string"/>; <see cref="bool"/> (via <c>true/yes/on</c> and
///       <c>false/no/off</c>); any <see langword="enum"/> (case-insensitive,
///       kebab-case mapped to PascalCase, flags separated by commas).
///       Strings do <b>not</b> convert to numeric types.
///     </description>
///   </item>
///   <item>
///     <term><see cref="ExpressionValue"/></term>
///     <description>
///       If an <c>expressionEvaluator</c> callback is supplied, it is invoked
///       with the raw expression text and target type. Otherwise the expression
///       text is treated as a plain string and the <see cref="StringValue"/>
///       conversion rules apply.
///     </description>
///   </item>
///   <item>
///     <term><see cref="LongValue"/></term>
///     <description>
///       Any numeric type (<see cref="int"/>, <see cref="short"/>, <see cref="long"/>,
///       <see cref="byte"/>, <see cref="float"/>, <see cref="double"/>,
///       <see cref="decimal"/>, etc.) using <see langword="checked"/> arithmetic.
///       Also converts to <see langword="enum"/> via its underlying integer value.
///     </description>
///   </item>
///   <item>
///     <term><see cref="DoubleValue"/></term>
///     <description>
///       <see cref="double"/>, <see cref="float"/>, <see cref="decimal"/>.
///       Also converts to integer types <b>only</b> when the value has no
///       fractional part (e.g. <c>3.0</c> → <c>3</c> is allowed,
///       <c>3.14</c> → <c>int</c> throws).
///     </description>
///   </item>
///   <item>
///     <term><see cref="BoolValue"/></term>
///     <description><see cref="bool"/> only.</description>
///   </item>
/// </list>
/// <para>
/// Overloads accepting a <see cref="SourceLocation"/> wrap conversion failures
/// in <see cref="ConfigurationConversionException"/> with precise file/line info.
/// Overloads without a location throw <see cref="InvalidOperationException"/>
/// or <see cref="OverflowException"/> directly.
/// </para>
/// <para>
/// The public <see cref="ConfigValue"/> kinds listed above are what appear in a
/// preprocessed <see cref="ConfigDocument"/>. (Grammar-only or internal
/// <c>InterpolatedStringValue</c> sources are turned into
/// <see cref="StringValue"/> when values are materialized for conversion.)
/// </para>
/// </remarks>
public static class ConfigValueConverter
{
    /// <summary>
    /// Converts a <see cref="ConfigValue"/> to the specified CLR type.
    /// On failure, throws <see cref="ConfigurationConversionException"/>
    /// with the source location from the configuration file.
    /// </summary>
    /// <param name="value">The configuration value to convert.</param>
    /// <param name="destinationType">The target CLR type. Nullable wrappers are unwrapped automatically.</param>
    /// <param name="location">The source location of the value in the configuration file.</param>
    /// <param name="expressionEvaluator">
    /// Optional callback invoked when an <see cref="ExpressionValue"/> must be converted.
    /// </param>
    /// <returns>The converted value, boxed as <see cref="object"/>.</returns>
    /// <exception cref="ConfigurationConversionException">
    /// The conversion is not supported, would lose data, or overflows the target type.
    /// </exception>
    public static object ConvertTo(
        ConfigValue value,
        Type destinationType,
        SourceLocation location,
        Func<string, Type, object>? expressionEvaluator = null)
    {
        ArgumentNullException.ThrowIfNull(location);

        var targetType = Nullable.GetUnderlyingType(destinationType) ?? destinationType;

        try
        {
            return ConvertTo(value, destinationType, expressionEvaluator);
        }
        catch (Exception ex) when (ex is InvalidOperationException or OverflowException)
        {
            throw new ConfigurationConversionException(
                ex.Message, location.FilePath, location.Line, targetType, ex);
        }
    }

    /// <summary>
    /// Converts a <see cref="ConfigValue"/> to <typeparamref name="T"/>.
    /// On failure, throws <see cref="ConfigurationConversionException"/>
    /// with the source location from the configuration file.
    /// </summary>
    /// <typeparam name="T">The target CLR type.</typeparam>
    /// <param name="value">The configuration value to convert.</param>
    /// <param name="location">The source location of the value in the configuration file.</param>
    /// <param name="expressionEvaluator">
    /// Optional callback for <see cref="ExpressionValue"/> evaluation.
    /// </param>
    /// <returns>The converted value.</returns>
    /// <exception cref="ConfigurationConversionException">
    /// The conversion is not supported, would lose data, or overflows the target type.
    /// </exception>
    public static T ConvertTo<T>(
        ConfigValue value,
        SourceLocation location,
        Func<string, Type, object>? expressionEvaluator = null)
    {
        return (T)ConvertTo(value, typeof(T), location, expressionEvaluator);
    }

    /// <summary>
    /// Converts a <see cref="ConfigValue"/> to the specified CLR type.
    /// </summary>
    /// <param name="value">The configuration value to convert.</param>
    /// <param name="destinationType">The target CLR type. Nullable wrappers are unwrapped automatically.</param>
    /// <param name="expressionEvaluator">
    /// Optional callback invoked when an <see cref="ExpressionValue"/> must be converted.
    /// Receives the raw expression text and the target type; must return an instance
    /// of that type. When <see langword="null"/>, expressions are treated as plain strings.
    /// </param>
    /// <returns>The converted value, boxed as <see cref="object"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// The conversion is not supported or would lose data.
    /// </exception>
    /// <exception cref="OverflowException">
    /// A numeric conversion overflows the target type.
    /// </exception>
    public static object ConvertTo(
        ConfigValue value,
        Type destinationType,
        Func<string, Type, object>? expressionEvaluator = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(destinationType);

        var targetType = Nullable.GetUnderlyingType(destinationType) ?? destinationType;

        return value switch
        {
            ExpressionValue ev => ConvertExpression(ev.Expression, targetType, expressionEvaluator),
            StringValue sv => ConvertString(sv.Value, targetType),
            LongValue lv => ConvertLong(lv.Value, targetType),
            DoubleValue dv => ConvertDouble(dv.Value, targetType),
            BoolValue bv => ConvertBool(bv.Value, targetType),
            _ => throw new InvalidOperationException(
                $"Cannot convert {value.GetType().Name} to {targetType.Name}.")
        };
    }

    /// <summary>
    /// Converts a <see cref="ConfigValue"/> to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target CLR type.</typeparam>
    /// <param name="value">The configuration value to convert.</param>
    /// <param name="expressionEvaluator">
    /// Optional callback for <see cref="ExpressionValue"/> evaluation.
    /// </param>
    /// <returns>The converted value.</returns>
    public static T ConvertTo<T>(
        ConfigValue value,
        Func<string, Type, object>? expressionEvaluator = null)
    {
        return (T)ConvertTo(value, typeof(T), expressionEvaluator);
    }

    private static object ConvertExpression(
        string expression, Type targetType, Func<string, Type, object>? evaluator)
    {
        if (evaluator is not null)
            return evaluator(expression, targetType);

        return ConvertString(expression, targetType);
    }

    private static object ConvertString(string value, Type targetType)
    {
        if (targetType == typeof(string))
            return value;

        if (targetType == typeof(bool))
            return ParseBoolString(value);

        if (targetType.IsEnum)
            return ParseEnumString(value, targetType);

        throw new InvalidOperationException(
            $"Cannot convert string to {targetType.Name}.");
    }

    private static bool ParseBoolString(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "true" or "yes" or "on" => true,
            "false" or "no" or "off" => false,
            _ => throw new InvalidOperationException(
                $"Cannot convert string \"{value}\" to Boolean. " +
                "Expected one of: true, yes, on, false, no, off.")
        };
    }

    private static object ParseEnumString(string value, Type enumType)
    {
        if (value.Contains(','))
        {
            long combined = 0;
            foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                combined |= Convert.ToInt64(ParseSingleEnumValue(part, enumType), CultureInfo.InvariantCulture);
            }
            return Enum.ToObject(enumType, combined);
        }

        return ParseSingleEnumValue(value, enumType);
    }

    private static object ParseSingleEnumValue(string value, Type enumType)
    {
        if (Enum.TryParse(enumType, value, ignoreCase: true, out var result))
            return result!;

        var pascalName = KebabToPascal(value);
        if (Enum.TryParse(enumType, pascalName, ignoreCase: false, out result))
            return result!;

        throw new InvalidOperationException(
            $"Cannot convert string \"{value}\" to {enumType.Name}.");
    }

    private static string KebabToPascal(string kebab)
    {
        if (!kebab.Contains('-'))
            return kebab;

        var sb = new StringBuilder(kebab.Length);
        bool capitalizeNext = true;

        foreach (var c in kebab)
        {
            if (c == '-')
            {
                capitalizeNext = true;
                continue;
            }

            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }

        return sb.ToString();
    }

    private static object ConvertLong(long value, Type targetType)
    {
        if (targetType.IsEnum)
            return Enum.ToObject(targetType, value);

        return Type.GetTypeCode(targetType) switch
        {
            TypeCode.Int64   => value,
            TypeCode.Int32   => checked((int)value),
            TypeCode.Int16   => checked((short)value),
            TypeCode.Byte    => checked((byte)value),
            TypeCode.SByte   => checked((sbyte)value),
            TypeCode.UInt16  => checked((ushort)value),
            TypeCode.UInt32  => checked((uint)value),
            TypeCode.UInt64  => checked((ulong)value),
            TypeCode.Single  => (float)value,
            TypeCode.Double  => (double)value,
            TypeCode.Decimal => (decimal)value,
            _ => throw new InvalidOperationException(
                $"Cannot convert long to {targetType.Name}.")
        };
    }

    private static object ConvertDouble(double value, Type targetType)
    {
        if (targetType == typeof(double))
            return value;

        if (targetType == typeof(float))
        {
            var f = (float)value;
            if (float.IsInfinity(f) && !double.IsInfinity(value))
                throw new OverflowException(
                    $"Value {value.ToString(CultureInfo.InvariantCulture)} is out of range for Single.");
            return f;
        }

        if (targetType == typeof(decimal))
            return (decimal)value;

        if (IsIntegerType(targetType) || targetType.IsEnum)
        {
            if (value != Math.Truncate(value))
                throw new InvalidOperationException(
                    $"Cannot convert {value.ToString(CultureInfo.InvariantCulture)} to {targetType.Name} " +
                    "without data loss (fractional part would be lost).");

            var longValue = checked((long)value);
            return targetType.IsEnum
                ? Enum.ToObject(targetType, longValue)
                : ConvertLong(longValue, targetType);
        }

        throw new InvalidOperationException(
            $"Cannot convert double to {targetType.Name}.");
    }

    private static object ConvertBool(bool value, Type targetType)
    {
        if (targetType == typeof(bool))
            return value;

        throw new InvalidOperationException(
            $"Cannot convert Boolean to {targetType.Name}.");
    }

    private static bool IsIntegerType(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(short) ||
        type == typeof(byte) || type == typeof(sbyte) || type == typeof(ushort) ||
        type == typeof(uint) || type == typeof(ulong);
}
