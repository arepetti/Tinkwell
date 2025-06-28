using System.Globalization;
using UnitsNet;

namespace Tinkwell.Store;

static class UnitHelpers
{
    public static Type? ParseQuantityType(string quantityType, bool throwOnError = true)
    {
        var exists = Quantity.Infos.Any(x => x.Name.Equals(quantityType, StringComparison.Ordinal));
        if (exists)
            return BuildType(typeof(Quantity).Namespace!, quantityType, throwOnError);

        if (throwOnError)
            throw new ArgumentException($"Unknown or invalid quantity type '{quantityType}'.", nameof(quantityType));

        return null;
    }

    private static Type BuildType(string @namespace, string typeName, bool throwOnError = true)
    {
        var assemblyName = typeof(Quantity).Assembly.FullName;
        return Type.GetType($"{@namespace}.{typeName},{assemblyName}", throwOnError)!;
    }

    public static Enum ParseUnit(string quantityType, string? unit)
    {
        var type = BuildType(typeof(UnitsNet.Units.ScalarUnit).Namespace!, $"{quantityType}Unit");
        var possibleValues = Enum.GetValues(type);

        if (string.IsNullOrWhiteSpace(unit))
        {
            if (possibleValues.Length == 1)
                return (Enum)possibleValues.GetValue(0)!;

            ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        }

        return (Enum)Enum.Parse(type, unit, ignoreCase: true);
    }

    public static bool IsValidUnit(string unitTypeName, string? unitName)
    {
        var unitType = ParseQuantityType(unitTypeName, throwOnError: false);
        if (unitType is null)
            return false;

        // Scalar unit types can have an empty/null unit name, as they represent a dimensionless quantity.
        if (string.IsNullOrWhiteSpace(unitName) && unitType == typeof(Scalar))
            return true;

        return UnitParser.Default.TryParse(unitName, unitType, CultureInfo.InvariantCulture, out _);
    }

    public static IQuantity Parse(QuantityMetadata metadata, string value)
    {
        var valueWithInputUnit = Quantity.Parse(CultureInfo.InvariantCulture, metadata.ResolveQuantityType(), value);
        var valueWithTargetUnit = valueWithInputUnit.ToUnit(metadata.ResolveUnit());

        return valueWithTargetUnit;
    }
}
