using System.Globalization;
using Tinkwell.Services;

namespace Tinkwell.Measures;

public static class StoreExtensions
{
    public static object ToObject(this StoreValue value)
        => value.HasNumberValue ? value.NumberValue : value.StringValue;

    public static string FormatAsString(this StoreValue value)
    {
        if (value.HasNumberValue)
            return value.NumberValue.ToString("G", CultureInfo.InvariantCulture);

        return value.StringValue;
    }
}