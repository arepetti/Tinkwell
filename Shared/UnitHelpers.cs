// ******************************************************************************
// DO NOT CHANGE THIS FILE! It's an helper file shared among multiple projects.
//
// Also, you MUST always add this file to your project as a link. Do not make
// a copy of this, it's used to share some code without creating a library with
// a massive list of dependencies for all the gRPC services we might need in
// multiple places.
// ******************************************************************************

using UnitsNet;

namespace Tinkwell;

internal static class UnitHelpers
{
    public static Enum ParseUnit(string quantityType, string? unit)
    {
        var type = FindQuantityInfoByName(quantityType, throwIfNotFound: true)!.UnitType;
        var possibleValues = global::System.Enum.GetValues(type);

        if (string.IsNullOrWhiteSpace(unit))
        {
            if (possibleValues.Length == 1)
                return (global::System.Enum)possibleValues.GetValue(0)!;

            global::System.ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        }

        return (global::System.Enum)global::System.Enum.Parse(type, unit, ignoreCase: true);
    }

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

    public static global::UnitsNet.IQuantity Parse(string quantityType, string? unit, string value)
    {
        var quantityTypeType = FindQuantityInfoByName(quantityType, throwIfNotFound: true)!.ValueType;
        var unitType = ParseUnit(quantityType, unit);
        var valueWithInputUnit = global::UnitsNet.Quantity.Parse(global::System.Globalization.CultureInfo.InvariantCulture, quantityTypeType, value);
        var valueWithTargetUnit = valueWithInputUnit.ToUnit(unitType);

        return valueWithTargetUnit;
    }

    public static global::UnitsNet.IQuantity From(string quantityType, string? unit, double value)
    {
        var quantityTypeType = FindQuantityInfoByName(quantityType, throwIfNotFound: true)!.ValueType;
        var unitType = ParseUnit(quantityType, unit);
        global::UnitsNet.QuantityValue quantityValue = value;
        return global::UnitsNet.Quantity.From(value, unitType!);
    }

    public static bool TryGetQuantityValue(object value, out global::UnitsNet.QuantityValue quantityValue)
    {
        quantityValue = global::UnitsNet.QuantityValue.Zero;
        if (TryAssign<byte>(value, () => (byte)value, ref quantityValue)) return true;
        if (TryAssign<sbyte>(value, () => (sbyte)value, ref quantityValue)) return true;
        if (TryAssign<short>(value, () => (short)value, ref quantityValue)) return true;
        if (TryAssign<ushort>(value, () => (ushort)value, ref quantityValue)) return true;
        if (TryAssign<int>(value, () => (int)value, ref quantityValue)) return true;
        if (TryAssign<uint>(value, () => (uint)value, ref quantityValue)) return true;
        if (TryAssign<long>(value, () => (long)value, ref quantityValue)) return true;
        if (TryAssign<ulong>(value, () => (double)value, ref quantityValue)) return true;
        if (TryAssign<float>(value, () => (float)value, ref quantityValue)) return true;
        if (TryAssign<double>(value, () => (double)value, ref quantityValue)) return true;
        if (TryAssign<decimal>(value, () => (decimal)value, ref quantityValue)) return true;
        return false;
    }

    public static global::UnitsNet.IQuantity Round(global::UnitsNet.IQuantity quantity, int decimalPlaces)
    {
        var value = quantity.Value;
        if (quantity.Value.IsDecimal)
            value = global::System.Math.Round((decimal)value, decimalPlaces);
        else
            value = global::System.Math.Round((double)value, decimalPlaces);

        return global::UnitsNet.Quantity.From(value, quantity.Unit);
    }

    private static global::UnitsNet.QuantityInfo? FindQuantityInfoByName(string quantityType, bool throwIfNotFound = true)
    {
        var info = global::UnitsNet.Quantity.Infos.FirstOrDefault(x => x.Name.Equals(quantityType, global::System.StringComparison.Ordinal));
        if (info is not null)
            return info;

        if (throwIfNotFound)
            throw new global::System.ArgumentException($"Unknown or invalid quantity type '{quantityType}'.", nameof(quantityType));

        return null;
    }

    private static bool TryAssign<T>(object value, global::System.Func<global::UnitsNet.QuantityValue> convert, ref global::UnitsNet.QuantityValue quantityValue)
    {
        if (value is T)
        {
            quantityValue = convert();
            return true;
        }

        return false;
    }
}
