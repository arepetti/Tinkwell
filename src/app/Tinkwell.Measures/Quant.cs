using System.Globalization;
using UnitsNet;

namespace Tinkwell.Measures;

/// <summary>
/// Utility methods for working with UnitsNet quantities: parsing, converting,
/// rounding, and validation.
/// </summary>
public static class Quant
{
    /// <summary>
    /// Parses a unit enum value from its name within a given quantity type.
    /// </summary>
    public static Enum ParseUnit(string quantityType, string? unit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quantityType, nameof(quantityType));

        var type = FindQuantityInfo(quantityType, throwIfNotFound: true)!.UnitType;
        var possibleValues = Enum.GetValues(type);

        if (string.IsNullOrWhiteSpace(unit))
        {
            if (possibleValues.Length == 1)
                return (Enum)possibleValues.GetValue(0)!;

            ArgumentException.ThrowIfNullOrWhiteSpace(unit, nameof(unit));
        }

        return (Enum)Enum.Parse(type, unit!, ignoreCase: true);
    }

    /// <summary>
    /// Checks whether a unit name is valid for a given quantity type.
    /// </summary>
    public static bool IsValidUnit(string quantityType, string? unitName)
    {
        try
        {
            ParseUnit(quantityType, unitName);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Parses a string value (e.g. "23.5 °C") and converts to the desired unit.
    /// </summary>
    public static IQuantity ParseAndConvert(string quantityType, string? desiredUnit, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quantityType, nameof(quantityType));

        var info = FindQuantityInfo(quantityType, throwIfNotFound: true)!;
        var unitType = ParseUnit(quantityType, desiredUnit);
        var parsed = Quantity.Parse(CultureInfo.InvariantCulture, info.ValueType, value);

        return parsed.ToUnit(unitType);
    }

    /// <summary>
    /// Parses a string value using the given quantity type.
    /// </summary>
    public static IQuantity Parse(string quantityType, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quantityType, nameof(quantityType));

        var info = FindQuantityInfo(quantityType, throwIfNotFound: true)!;

        try
        {
            return Quantity.Parse(CultureInfo.InvariantCulture, info.ValueType, value);
        }
        catch (UnitNotFoundException e)
        {
            throw new ArgumentException(e.Message, e);
        }
    }

    /// <summary>
    /// Creates a quantity from a numeric value and unit name.
    /// </summary>
    public static IQuantity From(string quantityType, string? unit, double value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quantityType, nameof(quantityType));

        var unitType = ParseUnit(quantityType, unit);
        return Quantity.From(value, unitType!);
    }

    /// <summary>
    /// Rounds a <see cref="MeasureValue"/> to the specified number of decimal places.
    /// </summary>
    public static MeasureValue Round(MeasureValue value, int decimalPlaces)
        => new(Round(value.AsQuantity(), decimalPlaces), value.Timestamp);

    /// <summary>
    /// Rounds a quantity to the specified number of decimal places.
    /// </summary>
    public static IQuantity Round(IQuantity quantity, int decimalPlaces)
    {
        ArgumentNullException.ThrowIfNull(quantity);

        if (decimalPlaces < 0)
            throw new ArgumentOutOfRangeException(nameof(decimalPlaces),
                "Decimal places must be zero or greater.");

        var rounded = Math.Round((double)quantity.Value, decimalPlaces);
        return Quantity.From(rounded, quantity.Unit);
    }

    private static QuantityInfo? FindQuantityInfo(string quantityType, bool throwIfNotFound)
    {
        var info = Quantity.Infos
            .FirstOrDefault(x => x.Name.Equals(quantityType, StringComparison.Ordinal));

        if (info is not null)
            return info;

        if (throwIfNotFound)
            throw new ArgumentException(
                $"Unknown or invalid quantity type '{quantityType}'.", nameof(quantityType));

        return null;
    }
}
