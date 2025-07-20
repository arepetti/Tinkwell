using System.Globalization;

namespace Tinkwell.Bootstrapper.Hosting;

/// <summary>
/// Provides extension methods for <see cref="IConfigurableHost"/> to retrieve strongly-typed properties.
/// </summary>
public static class IConfigurableHostExtensions
{
    /// <summary>
    /// Gets the value of a property as an <see cref="int"/>. Returns <paramref name="defaultValue"/> if the property is not found or cannot be converted.
    /// </summary>
    /// <param name="host">The host runner base instance.</param>
    /// <param name="name">The property name.</param>
    /// <param name="defaultValue">The default value to return if conversion fails.</param>
    /// <returns>The property value as an <see cref="int"/>.</returns>
    public static int GetPropertyInt32(this IConfigurableHost host, string name, int defaultValue)
    {
        var value = GetPropertyValue(host, name);
        if (value is null)
            return defaultValue;

        if (value is int)
            return (int)value;

        if (value is long)
            checked { return (int)(long)value; }

        try
        {
            var str = Convert.ToString(value, CultureInfo.InvariantCulture);
            return int.TryParse(str, CultureInfo.InvariantCulture, out int num) ? num : defaultValue;
        }
        catch (FormatException)
        {
            return defaultValue;
        }
        catch (InvalidCastException)
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Gets the value of a property as a <see cref="string"/>. Returns <paramref name="defaultValue"/> if the property is not found.
    /// </summary>
    /// <param name="host">The host runner base instance.</param>
    /// <param name="name">The property name.</param>
    /// <param name="defaultValue">The default value to return if the property is not found.</param>
    /// <returns>The property value as a <see cref="string"/>.</returns>
    public static string? GetPropertyString(this IConfigurableHost host, string name, string? defaultValue)
    {
        var value = GetPropertyValue(host, name);
        if (value is null)
            return defaultValue;

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the value of a property as a <see cref="bool"/>. Returns <paramref name="defaultValue"/> if the property is not found or cannot be converted.
    /// </summary>
    /// <param name="host">The host runner base instance.</param>
    /// <param name="name">The property name.</param>
    /// <param name="defaultValue">The default value to return if conversion fails.</param>
    /// <returns>The property value as a <see cref="bool"/>.</returns>
    public static bool GetPropertyBoolean(this IConfigurableHost host, string name, bool defaultValue)
    {
        var value = GetPropertyValue(host, name);
        if (value is null)
            return defaultValue;

        try
        {
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return defaultValue;
        }
        catch (InvalidCastException)
        {
            return defaultValue;
        }
    }

    private static object? GetPropertyValue(IConfigurableHost host, string name)
    {
        if (host.Properties.TryGetValue(name, out var value))
            return value;
     
        return null;
    }
}
