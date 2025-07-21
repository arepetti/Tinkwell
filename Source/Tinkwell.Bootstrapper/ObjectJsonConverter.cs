using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tinkwell;

/// <summary>
/// Special converter for <see cref="JsonSerializer"/> to workaround its default behaviour:
/// when deserializing a dictionary <c>(Dictionary{string, object}</c> as <c>object</c> it simply
/// returns the raw <c>JsonElement</c>. This converter changes that and returns a dictionary as expected.
/// </summary>
public sealed class ObjectJsonConverter : JsonConverter<object>
{
    /// <inheritedocs />
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString()!,
            JsonTokenType.Number => ReadNumber(reader),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null!,
            JsonTokenType.StartObject => ReadObject(ref reader, options),
            JsonTokenType.StartArray => ReadArray(ref reader, options),
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
        };
    }

    /// <inheritedocs />
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

    private object ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var dictionary = new Dictionary<string, object>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return dictionary;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName token");

            var propertyName = reader.GetString()!;
            reader.Read();
            dictionary[propertyName] = Read(ref reader, typeof(object), options)!;
        }

        throw new JsonException("Expected EndObject token");
    }

    private object ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var list = new List<object>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return list;

            list.Add(Read(ref reader, typeof(object), options)!);
        }

        throw new JsonException("Expected EndArray token");
    }
}