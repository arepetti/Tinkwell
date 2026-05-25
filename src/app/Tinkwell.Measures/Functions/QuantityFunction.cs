using System.Globalization;
using NCalc.Handlers;
using Tinkwell.Expressions.Functions;
using UnitsNet;

namespace Tinkwell.Measures.Functions;

/// <summary>
/// Converts a numeric value between physical units using UnitsNet abbreviations.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>quantity(value, unit)</c> — converts <c>value</c> from the
///     specified unit to the SI base unit of that quantity type
///     (e.g. <c>quantity(10, 'mV')</c> → <c>0.01</c> Volts).</item>
///   <item><c>quantity(value, fromUnit, toUnit)</c> — converts between two
///     explicit units (e.g. <c>quantity(10, 'mV', 'kV')</c> → <c>0.00001</c>).</item>
/// </list>
/// <para>
/// Unit strings use UnitsNet abbreviations. The user is responsible for
/// ensuring unit compatibility in surrounding expressions.
/// </para>
/// </remarks>
public sealed class QuantityFunction : ExpressionFunction
{
    public override string Name => "quantity";

    public override object? Invoke(FunctionArgs args)
    {
        var values = args.EvaluateParameters();
        return values.Length switch
        {
            2 => ConvertToBase(
                ChangeType<double>(values[0]),
                ChangeType<string>(values[1])),
            3 => ConvertExplicit(
                ChangeType<double>(values[0]),
                ChangeType<string>(values[1]),
                ChangeType<string>(values[2])),
            _ => throw new ArgumentException(
                $"quantity() requires 2 or 3 arguments, received {values.Length}.")
        };
    }

    private static double ConvertToBase(double value, string fromUnit)
    {
        var (unitEnum, baseUnit) = ResolveUnit(fromUnit);
        var q = Quantity.From(value, unitEnum);
        return (double)q.ToUnit(baseUnit).Value;
    }

    private static double ConvertExplicit(double value, string fromUnit, string toUnit)
    {
        var fromText = "0 " + fromUnit;
        var toText = "0 " + toUnit;

        foreach (var qi in Quantity.Infos)
        {
            try
            {
                var fromParsed = Quantity.Parse(CultureInfo.InvariantCulture, qi.ValueType, fromText);
                var toParsed = Quantity.Parse(CultureInfo.InvariantCulture, qi.ValueType, toText);
                var q = Quantity.From(value, fromParsed.Unit);
                return (double)q.ToUnit(toParsed.Unit).Value;
            }
            catch
            {
                // Not a matching quantity type for both units
            }
        }

        throw new ArgumentException(
            $"Cannot find a common quantity type for '{fromUnit}' and '{toUnit}'.");
    }

    private static (Enum Unit, Enum BaseUnit) ResolveUnit(string abbreviation)
    {
        var text = "0 " + abbreviation;
        foreach (var qi in Quantity.Infos)
        {
            try
            {
                var parsed = Quantity.Parse(CultureInfo.InvariantCulture, qi.ValueType, text);
                return (parsed.Unit, qi.Zero.Unit);
            }
            catch
            {
                // Not a unit for this quantity type
            }
        }

        throw new ArgumentException($"Unknown unit abbreviation '{abbreviation}'.");
    }
}
