using System.Globalization;

namespace Tinkwell.Bootstrapper;

/// <summary>
/// Provides extension methods for <see cref="ITinkwellHostRunnerBase"/> to retrieve strongly-typed properties.
/// </summary>
public static class ITinkwellHostRunnerBaseExtensions
{
    /// <summary>
    /// Gets the value of a property as an <see cref="int"/>. Returns <paramref name="defaultValue"/> if the property is not found or cannot be converted.
    /// </summary>
    /// <param name="host">The host runner base instance.</param>
    /// <param name="name">The property name.</param>
    /// <param name="defaultValue">The default value to return if conversion fails.</param>
    /// <returns>The property value as an <see cref="int"/>.</returns>
    public static int GetPropertyInt32(this ITinkwellHostRunnerBase host, string name, int defaultValue)
    {
        if (host.Properties.TryGetValue(name, out var obj))
        {
            if (obj is int)
                return (int)obj;

            try
            {
                var str = Convert.ToString(obj, CultureInfo.InvariantCulture);
                return int.TryParse(str, CultureInfo.InvariantCulture, out int value) ? value : defaultValue;
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

        return defaultValue;
    }

    /// <summary>
    /// Gets the value of a property as a <see cref="string"/>. Returns <paramref name="defaultValue"/> if the property is not found.
    /// </summary>
    /// <param name="host">The host runner base instance.</param>
    /// <param name="name">The property name.</param>
    /// <param name="defaultValue">The default value to return if the property is not found.</param>
    /// <returns>The property value as a <see cref="string"/>.</returns>
    public static string? GetPropertyString(this ITinkwellHostRunnerBase host, string name, string? defaultValue)
    {
        if (host.Properties.TryGetValue(name, out var obj))
            return Convert.ToString(obj, CultureInfo.InvariantCulture);

        return defaultValue;
    }

    /// <summary>
    /// Gets the value of a property as a <see cref="bool"/>. Returns <paramref name="defaultValue"/> if the property is not found or cannot be converted.
    /// </summary>
    /// <param name="host">The host runner base instance.</param>
    /// <param name="name">The property name.</param>
    /// <param name="defaultValue">The default value to return if conversion fails.</param>
    /// <returns>The property value as a <see cref="bool"/>.</returns>
    public static bool GetPropertyBoolean(this ITinkwellHostRunnerBase host, string name, bool defaultValue)
    {
        if (host.Properties.TryGetValue(name, out var obj))
        {
            try
            {
                return Convert.ToBoolean(obj, CultureInfo.InvariantCulture);
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

        return defaultValue;
    }
}
