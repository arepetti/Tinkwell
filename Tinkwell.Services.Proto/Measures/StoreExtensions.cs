using Tinkwell.Services;

namespace Tinkwell.Measures;

public static class StoreExtensions
{
    public static object ToObject(this StoreValue value)
        => value.HasNumberValue ? value.NumberValue : value.StringValue;
}