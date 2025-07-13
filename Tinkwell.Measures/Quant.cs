using System.Globalization;
using UnitsNet;

namespace Tinkwell.Measures;

/// <summary>
/// Provides utility methods for working with quantities.
/// </summary>
public static class Quant
{
    /// <summary>
    /// Parses a unit from a string.
    /// </summary>
    /// <param name="quantityType">The type of the quantity.</param>
    /// <param name="unit">The unit to parse.</param>
    /// <returns>The parsed unit.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="quantityType"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="quantityType"/> is not valid.</exception>"
    public static Enum ParseUnit(string quantityType, string? unit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quantityType, nameof(quantityType));

        var type = FindQuantityInfoByName(quantityType, throwIfNotFound: true)!.UnitType;
        var possibleValues = Enum.GetValues(type);

        if (string.IsNullOrWhiteSpace(unit))
        {
            if (possibleValues.Length == 1)
                return (Enum)possibleValues.GetValue(0)!;

            ArgumentException.ThrowIfNullOrWhiteSpace(unit, nameof(unit));
        }

        return (Enum)Enum.Parse(type, unit, ignoreCase: true);
    }

    /// <summary>
    /// Checks if a unit is valid.
    /// </summary>
    /// <param name="quantityType">The type of the quantity.</param>
    /// <param name="unitName">The name of the unit.</param>
    /// <returns>true if the unit is valid; otherwise, false.</returns>
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
    /// Parses and converts a value to a specified unit.
    /// </summary>
    /// <param name="quantityType">The type of the quantity.</param>
    /// <param name="desiredUnit">The desired unit.</param>
    /// <param name="value">The value to parse and convert.</param>
    /// <returns>The parsed and converted quantity.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="quantityType"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="quantityType"/> is not valid.</exception>"
    public static IQuantity ParseAndConvert(string quantityType, string? desiredUnit, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quantityType, nameof(quantityType));

        var quantityTypeType = FindQuantityInfoByName(quantityType, throwIfNotFound: true)!.ValueType;
        var unitType = ParseUnit(quantityType, desiredUnit);
        var valueWithInputUnit = Quantity.Parse(CultureInfo.InvariantCulture, quantityTypeType, value);
        var valueWithTargetUnit = valueWithInputUnit.ToUnit(unitType);

        return valueWithTargetUnit;
    }

    /// <summary>
    /// Parses a value.
    /// </summary>
    /// <param name="quantityType">The type of the quantity.</param>
    /// <param name="value">The value to parse.</param>
    /// <returns>The parsed quantity.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="quantityType"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// When <paramref name="quantityType"/> is not valid or the value cannot be parsed.
    /// </exception>"
    public static IQuantity Parse(string quantityType, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quantityType, nameof(quantityType));

        var quantityTypeType = FindQuantityInfoByName(quantityType, throwIfNotFound: true)!.ValueType;

        try
        {
            return Quantity.Parse(CultureInfo.InvariantCulture, quantityTypeType, value);
        }
        catch (UnitNotFoundException e)
        {
            throw new ArgumentException(e.Message, e);
        }
    }

    /// <summary>
    /// Creates a quantity from a value and a unit.
    /// </summary>
    /// <param name="quantityType">The type of the quantity.</param>
    /// <param name="unit">The unit of the quantity.</param>
    /// <param name="value">The value of the quantity.</param>
    /// <returns>The created quantity.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="quantityType"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="quantityType"/> is not valid.</exception>"
    public static IQuantity From(string quantityType, string? unit, double value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quantityType, nameof(quantityType));

        var quantityTypeType = FindQuantityInfoByName(quantityType, throwIfNotFound: true)!.ValueType;
        var unitType = ParseUnit(quantityType, unit);
        return Quantity.From(value, unitType!);
    }

    /// <summary>
    /// Rounds a measure value to a specified number of decimal places.
    /// </summary>
    /// <param name="value">The value to round.</param>
    /// <param name="decimalPlaces">The number of decimal places to round to.</param>
    /// <returns>The rounded measure value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="decimalPlaces"/> is less than zero.</exception>"
    public static MeasureValue Round(MeasureValue value, int decimalPlaces)
        => new MeasureValue(Round(value.AsQuantity(), decimalPlaces), value.Timestamp);

    /// <summary>
    /// Rounds a quantity to a specified number of decimal places.
    /// </summary>
    /// <param name="quantity">The quantity to round.</param>
    /// <param name="decimalPlaces">The number of decimal places to round to.</param>
    /// <returns>The rounded quantity.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="quantity"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="decimalPlaces"/> is less than zero.</exception>"
    public static IQuantity Round(IQuantity quantity, int decimalPlaces)
    {
        ArgumentNullException.ThrowIfNull(quantity, nameof(quantity));

        if (decimalPlaces < 0)
            throw new ArgumentOutOfRangeException(nameof(decimalPlaces), "Decimal places must be zero or greater.");

        var value = quantity.Value;
        if (quantity.Value.IsDecimal)
            value = Math.Round((decimal)value, decimalPlaces);
        else
            value = Math.Round((double)value, decimalPlaces);

        return Quantity.From(value, quantity.Unit);
    }

    private static QuantityInfo? FindQuantityInfoByName(string quantityType, bool throwIfNotFound = true)
    {
        var info = Quantity.Infos.FirstOrDefault(x => x.Name.Equals(quantityType, StringComparison.Ordinal));
        if (info is not null)
            return info;

        if (throwIfNotFound)
            throw new ArgumentException($"Unknown or invalid quantity type '{quantityType}'.", nameof(quantityType));

        return null;
    }
}
