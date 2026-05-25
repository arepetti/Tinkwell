using System.Text.Json;
using NCalc.Handlers;

namespace Tinkwell.Expressions.Functions.Builtins;

/// <summary>
/// <c>json_path(json, path)</c> — Navigates JSON by dot path; returns a <c>JsonElement</c> clone of the value.
/// </summary>
sealed class JsonPath : BinaryFunction<string, string>
{
    protected override object? Call(string json, string path)
    {
        using var doc = JsonDocument.Parse(json);
        return NavigateJsonPath(doc.RootElement, path).Clone();
    }

    public static JsonElement NavigateJsonPath(JsonElement current, string path)
    {
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index))
            {
                if (index >= 0 && index < current.GetArrayLength())
                    current = current[index];
                else
                    throw new ArgumentException(
                        $"JSON path index '{segment}' is out of bounds for path '{path}'.");
            }
            else if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var next))
            {
                current = next;
            }
            else
            {
                throw new ArgumentException(
                    $"JSON path segment '{segment}' not found or not valid for path '{path}'.");
            }
        }

        return current;
    }
}

/// <summary>
/// <c>json_value(json, path)</c> — Navigates JSON and returns a scalar (string, number, bool, or null) or string fallback.
/// </summary>
sealed class JsonValue : BinaryFunction<string, string>
{
    protected override object? Call(string json, string path)
    {
        using var doc = JsonDocument.Parse(json);
        var current = JsonPath.NavigateJsonPath(doc.RootElement, path);

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

/// <summary>
/// <c>make_json(k1, v1, k2, v2, ...)</c> — Builds a JSON object (flat dictionary) from key/value pairs and serializes it.
/// </summary>
sealed class MakeJson : ExpressionFunction
{
    public override string Name => "make_json";

    public override object? Invoke(FunctionArgs args)
    {
        if (args.Parameters.Length % 2 != 0)
            throw new ArgumentException(
                $"Function {Name}() requires an even number of arguments (key/value pairs), received {args.Parameters.Length}.");

        var parameters = args.EvaluateParameters();
        var dictionary = new Dictionary<string, object?>();
        for (var i=0; i < parameters.Length; i += 2)
            dictionary[ChangeType<string>(parameters[i])] = parameters[i + 1];

        return JsonSerializer.Serialize(dictionary);
    }
}
