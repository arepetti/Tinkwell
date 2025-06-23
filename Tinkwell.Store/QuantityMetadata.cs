using Tinkwell.Store.Storage;
using UnitsNet;

namespace Tinkwell.Store;

public sealed class QuantityMetadata : IStorageMetadata
{
    public required string Name 
    {
        get => _name;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Name));
            _name = value;
        }
    }

    public TimeSpan? Ttl
    {
        get => _ttl;
        set
        {
            if (value is not null && value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "TTL must be greater than zero.");

            _ttl = value;
        }
    }

    public string QuantityType
    {
        get => _quantityType;
        set
        {
            if (string.Equals(_quantityType, value, StringComparison.OrdinalIgnoreCase))
                return;

            _quantityType = value;
            _resolvedQuantityType = null;
        }
    }

    public string? Unit 
    { 
        get => _unit;
        set
        {
            if (string.Equals(_unit, value, StringComparison.OrdinalIgnoreCase))
                return;

            _unit = value;
            _resolvedUnit = null;
        }
    }

    string IStorageMetadata.Key => _name;

    internal Type ResolveQuantityType()
    {
        _resolvedQuantityType ??= UnitHelpers.ParseQuantityType(QuantityType);

        return _resolvedQuantityType!;
    }

    internal Enum ResolveUnit()
    {
        _resolvedUnit ??= UnitHelpers.ParseUnit(QuantityType, Unit);

        return _resolvedUnit;
    }

    private string _name = "";
    private TimeSpan? _ttl;
    private string? _unit;
    private string _quantityType = nameof(Scalar);
    private Type? _resolvedQuantityType;
    private Enum? _resolvedUnit;
}
