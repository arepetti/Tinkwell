using Google.Protobuf.WellKnownTypes;
using System.Globalization;

namespace Tinkwell.Services.Proto.Proxies;

/// <summary>
/// Helper methods for when working with the Store service.
/// </summary>
public static class StoreExtensions
{
    /// <summary>
    /// Converts a string value to a <see cref="StoreValue"/>.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <param name="timestamp">The optional timestamp. If null, <see cref="DateTime.UtcNow"/> is used.</param>
    /// <returns>A new <see cref="StoreValue"/> instance.</returns>
    public static StoreValue ToStoreValue(this string value, DateTime? timestamp = null)
    {
        var storeValue = new StoreValue();
        storeValue.Timestamp = Timestamp.FromDateTime(timestamp ?? DateTime.UtcNow);
        storeValue.StringValue = value;
        return storeValue;
    }

    /// <summary>
    /// Converts a double value to a <see cref="StoreValue"/>.
    /// </summary>
    /// <param name="value">The double value.</param>
    /// <param name="timestamp">The optional timestamp. If null, <see cref="DateTime.UtcNow"/> is used.</param>
    /// <returns>A new <see cref="StoreValue"/> instance.</returns>
    public static StoreValue ToStoreValue(this double value, DateTime? timestamp = null)
    {
        var storeValue = new StoreValue();
        storeValue.Timestamp = Timestamp.FromDateTime(timestamp ?? DateTime.UtcNow);
        storeValue.NumberValue = value;
        return storeValue;
    }

    /// <summary>
    /// Converts a <see cref="StoreValueList"/> to a generic dictionary.
    /// </summary>
    /// <param name="list">List of values to convert.</param>
    /// <returns>
    /// A dictionary where each entry is created the measure name as key and its value (either
    /// <c>double</c> or <c>string</c>) as value.
    /// </returns>
    public static Dictionary<string, object> ToDictionary(this StoreValueList list)
    {
        var result = new Dictionary<string, object>();
        foreach (var item in list.Items)
            result[item.Name] = ToObject(item.Value);

        return result;
    }

    /// <summary>
    /// Converts a <see cref="StoreValue"/> to its underlying <see cref="object"/> representation
    /// (either a double or a string).
    /// </summary>
    /// <param name="value">The <see cref="StoreValue"/> to convert.</param>
    /// <returns>The numeric or string value as an object or <c>null</c> if the value is undefined.</returns>
    public static object ToObject(this StoreValue value)
    {
        if (value.HasNumberValue)
            return value.NumberValue;
        
        if (value.HasStringValue)
            return value.StringValue;

        return null!;
    }

    /// <summary>
    /// Formats the <see cref="StoreValue"/> as a string, using invariant culture for numbers.
    /// </summary>
    /// <param name="value">The <see cref="StoreValue"/> to format.</param>
    /// <returns>A string representation of the value.</returns>
    public static string FormatAsString(this StoreValue value)
    {
        if (value.HasNumberValue)
            return value.NumberValue.ToString("G", CultureInfo.InvariantCulture);

        return value.StringValue;
    }

    /// <summary>
    /// Creates a <see cref="StoreFacade"/> from a <see cref="Store.StoreClient"/>.
    /// Use this facade class for a simplified access to the most common Store opertions.
    /// </summary>
    /// <param name="client">The gRPC store client.</param>
    /// <returns>A new <see cref="StoreFacade"/> instance.</returns>
    public static StoreFacade AsFacade(this Store.StoreClient client)
        => new StoreFacade(client);

    /// <summary>
    /// Checks if the measure definition has a specific attribute.
    /// </summary>
    /// <param name="definition">The measure definition.</param>
    /// <param name="attributes">The attribute(s) to check for.</param>
    /// <returns>True if the definition has the attribute, false otherwise.</returns>
    public static bool HasAttribute(this StoreDefinition definition, StoreMeasureAttributes attributes)
        => (definition.Attributes & (int)attributes) != 0;
}
