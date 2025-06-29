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
        }
    }

    public double? Minimum { get; set; }

    public double? Maximum { get; set; }

    public IReadOnlyList<string> Tags
    {
        get => _tags;
        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Tags));
            _tags = value;
        }
    }

    public string? Category { get; set; }

    public int? Precision { get; set; }

    string IStorageMetadata.Key => _name;

    private string _name = "";
    private TimeSpan? _ttl;
    private string? _unit;
    private string _quantityType = nameof(Scalar);
    private IReadOnlyList<string> _tags = new List<string>();
}
