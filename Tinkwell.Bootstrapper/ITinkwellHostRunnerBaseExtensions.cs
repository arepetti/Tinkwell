using System.Globalization;

namespace Tinkwell.Bootstrapper;

public static class ITinkwellHostRunnerBaseExtensions
{
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

    public static string? GetPropertyString(this ITinkwellHostRunnerBase host, string name, string? defaultValue)
    {
        if (host.Properties.TryGetValue(name, out var obj))
            return Convert.ToString(obj, CultureInfo.InvariantCulture);

        return defaultValue;
    }
}
