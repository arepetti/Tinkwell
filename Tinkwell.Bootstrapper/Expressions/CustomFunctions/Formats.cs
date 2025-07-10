using System.Text.Json;

namespace Tinkwell.Bootstrapper.Expressions.CustomFunctions;

class JsonPath : BinaryFunction<string, string>
{
    protected override object? Call(string json, string path)
    {
        using var doc = JsonDocument.Parse(json);
        return NavigateJsonPath(doc.RootElement, path);
    }

    protected static JsonElement NavigateJsonPath(JsonElement current, string path)
    {
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index))
            {
                if (index >= 0 && index < current.GetArrayLength())
                    current = current[index];
                else
                    throw new ArgumentException($"JSON path index '{segment}' is out of bounds for path '{path}'.");
            }
            else if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var next))
                current = next;
            else
                throw new ArgumentException($"JSON path segment '{segment}' not found or not valid for path '{path}'.");
        }

        return current;
    }
}

sealed class JsonValue : JsonPath
{
    protected override object? Call(string json, string path)
    {
        using var doc = JsonDocument.Parse(json);
        var current = NavigateJsonPath(doc.RootElement, path);

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.TryGetInt64(out var l) ? (object)l : current.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => current.ToString()
        };
    }
}

sealed class JsonGetString : UnaryFunction<JsonElement>
{
    protected override object? Call(JsonElement element)
        => element.ToString();
}

sealed class JsonGetInt : UnaryFunction<JsonElement>
{
    protected override object? Call(JsonElement element)
        => element.GetInt32();
}

sealed class JsonGetLong : UnaryFunction<JsonElement>
{
    protected override object? Call(JsonElement element)
        => element.GetInt64();
}

sealed class JsonGetSingle : UnaryFunction<JsonElement>
{
    protected override object? Call(JsonElement element)
        => element.GetSingle();
}

sealed class JsonGetDouble : UnaryFunction<JsonElement>
{
    protected override object? Call(JsonElement element)
        => element.GetDouble();
}

sealed class JsonGetBoolean : UnaryFunction<JsonElement>
{
    protected override object? Call(JsonElement element)
        => element.GetBoolean();
}
