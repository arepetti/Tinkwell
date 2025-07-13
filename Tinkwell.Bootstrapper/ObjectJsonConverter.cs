using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tinkwell;

public sealed class ObjectJsonConverter : JsonConverter<object>
{
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString()!,
            JsonTokenType.Number => ReadNumber(reader),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null!,
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
        };
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object), options);

    private static object ReadNumber(Utf8JsonReader reader)
    {
        if (reader.TryGetInt32(out int i))
            return i;

        if (reader.TryGetInt64(out long l))
            return l;

        return reader.GetDouble();
    }
}