using System.Text.Json;

namespace Tinkwell.Encoding;

/// <summary>
/// Encodes and decodes payloads using the SenML JSON format (RFC 8428).
/// </summary>
/// <remarks>
/// <para>
/// A SenML pack is a JSON array of records. Each record may carry a name (<c>n</c>) relative to a
/// base name (<c>bn</c>) defined in an earlier record, an optional timestamp (<c>t</c>) added to
/// any base timestamp (<c>bt</c>), and exactly one value field selected per <see cref="PayloadType"/>:
/// </para>
/// <list type="bullet">
///   <item><term><c>v</c></term><description>numeric value (used for <see cref="PayloadType.Float"/>, <see cref="PayloadType.Integer"/> and <see cref="PayloadType.Time"/> resource values).</description></item>
///   <item><term><c>vs</c></term><description>string value (<see cref="PayloadType.String"/>).</description></item>
///   <item><term><c>vb</c></term><description>boolean value (<see cref="PayloadType.Boolean"/>).</description></item>
///   <item><term><c>vd</c></term><description>base64-encoded binary value (<see cref="PayloadType.Opaque"/>).</description></item>
///   <item><term><c>vlo</c></term><description>OMA LwM2M object-link string <c>"&lt;objId&gt;:&lt;instId&gt;"</c> (<see cref="PayloadType.ObjectLink"/>).</description></item>
/// </list>
/// <para>
/// Records of type <see cref="PayloadType.None"/> are emitted without a value field (the SenML
/// value field is optional per RFC 8428, Section 4.4). An unknown <see cref="PayloadType"/> causes
/// <see cref="Encode"/> to throw <see cref="ArgumentException"/> rather than silently producing a
/// malformed record.
/// </para>
/// <para>
/// On decode, a JSON <c>v</c> field is mapped to <see cref="PayloadType.Integer"/> when the value
/// round-trips through <see cref="long"/> (i.e. <see cref="Utf8JsonReader.TryGetInt64"/> succeeds),
/// and to <see cref="PayloadType.Float"/> otherwise (this notably includes literals with a
/// fractional part, exponential form, or values outside the <see cref="long"/> range). SenML does
/// not carry the original semantic <see cref="PayloadType"/> on the wire, so a value encoded from
/// <see cref="PayloadType.Time"/> is decoded as a numeric value (typically
/// <see cref="PayloadType.Integer"/> when the encoder wrote it as Unix seconds); callers that need
/// <see cref="DateTimeOffset"/> semantics must reinterpret the decoded value themselves (e.g.
/// <c>DateTimeOffset.FromUnixTimeSeconds(value.AsLong())</c>).
/// </para>
/// <para>
/// Time resolution on decode follows RFC 8428, Section 4.5.3 with one pragmatic refinement:
/// </para>
/// <list type="bullet">
///   <item><description>When a per-record <c>t</c> is itself an absolute Unix timestamp (≥ 2^28),
///   it is taken as-is; any sticky <c>bt</c> is intentionally not added (otherwise an absolute
///   <c>bt</c> would shift an already-absolute <c>t</c> far into the future).</description></item>
///   <item><description>Otherwise the effective seconds value is <c>(bt ?? 0) + (t ?? 0)</c>; if that
///   sum is below 2<sup>28</sup> it is treated as a relative offset (in seconds) from a reference
///   "now" provided to <see cref="Decode(System.ReadOnlySpan{byte}, System.DateTimeOffset?)"/>
///   (default <see cref="DateTimeOffset.UtcNow"/>); otherwise it is treated as absolute Unix seconds.</description></item>
/// </list>
/// <para>
/// The decoder uses <see cref="Utf8JsonReader"/> and does not materialize a JSON DOM. For payloads
/// from untrusted sources, callers should still bound the input size at the transport layer.
/// </para>
/// </remarks>
public static class SenmlJsonCodec
{
    /// <summary>
    /// Encodes resource values as a SenML JSON pack (RFC 8428, Section 4).
    /// </summary>
    /// <param name="objectId">The LwM2M object ID used to build the base name (<c>bn = "/objectId/instanceId/"</c>).</param>
    /// <param name="instanceId">The LwM2M object instance ID used to build the base name.</param>
    /// <param name="records">The records to encode (in order).</param>
    /// <returns>A newly allocated array of UTF-8 JSON bytes.</returns>
    /// <exception cref="ArgumentException">A record carries an unsupported <see cref="PayloadType"/>.</exception>
    public static byte[] Encode(
        int objectId, int instanceId,
        IReadOnlyList<SenmlRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var baseName = $"/{objectId}/{instanceId}/";

        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);

        writer.WriteStartArray();

        for (int i=0; i < records.Count; ++i)
        {
            var record = records[i];
            writer.WriteStartObject();

            // RFC 8428, Section 4.3: base name on first record.
            if (i == 0)
                writer.WriteString("bn", baseName);

            // RFC 8428, Section 4.5.1: name relative to base name.
            writer.WriteString("n", record.ResourceId.ToString(System.Globalization.CultureInfo.InvariantCulture));

            WriteValue(writer, record);

            // RFC 8428, Section 4.5.3: optional sample time. We always emit absolute Unix seconds
            // (>= 2^28 for any current time), so no base-time computation is required on the wire.
            if (record.Timestamp.HasValue)
                writer.WriteNumber("t", record.Timestamp.Value.ToUnixTimeSeconds());

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Decodes a SenML JSON pack into resource values (RFC 8428, Section 4) using
    /// <see cref="DateTimeOffset.UtcNow"/> as the reference for relative timestamps.
    /// </summary>
    /// <param name="json">The UTF-8 encoded JSON array.</param>
    /// <returns>The decoded records, with names resolved against any base name (<c>bn</c>).</returns>
    /// <exception cref="FormatException">The JSON is not a SenML pack or carries malformed values.</exception>
    public static IReadOnlyList<DecodedSenmlRecord> Decode(ReadOnlySpan<byte> json)
        => Decode(json, now: null);

    /// <summary>
    /// Decodes a SenML JSON pack from a <see cref="string"/> using
    /// <see cref="DateTimeOffset.UtcNow"/> as the reference for relative timestamps.
    /// </summary>
    /// <remarks>The string is UTF-8-encoded internally; prefer the <see cref="ReadOnlySpan{Byte}"/>
    /// overloads if you already have bytes from the network.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The JSON is not a SenML pack or carries malformed values.</exception>
    public static IReadOnlyList<DecodedSenmlRecord> Decode(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Decode(System.Text.Encoding.UTF8.GetBytes(json), now: null);
    }

    /// <summary>
    /// Decodes a SenML JSON pack from a <see cref="string"/> with a caller-supplied reference
    /// "now" for relative timestamps (RFC 8428, Section 4.5.3).
    /// </summary>
    /// <param name="json">The JSON pack as a string.</param>
    /// <param name="now">The reference time for relative timestamps; <see langword="null"/> means <see cref="DateTimeOffset.UtcNow"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The JSON is not a SenML pack or carries malformed values.</exception>
    public static IReadOnlyList<DecodedSenmlRecord> Decode(string json, DateTimeOffset? now)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Decode(System.Text.Encoding.UTF8.GetBytes(json), now);
    }

    /// <summary>
    /// Decodes a SenML JSON pack into resource values (RFC 8428, Section 4).
    /// </summary>
    /// <param name="json">The UTF-8 encoded JSON array.</param>
    /// <param name="now">
    /// The reference time for relative timestamps (RFC 8428, Section 4.5.3). When
    /// <see langword="null"/>, <see cref="DateTimeOffset.UtcNow"/> is used.
    /// </param>
    /// <returns>The decoded records, with names resolved against any base name (<c>bn</c>).</returns>
    /// <exception cref="FormatException">The JSON is not a SenML pack or carries malformed values.</exception>
    public static IReadOnlyList<DecodedSenmlRecord> Decode(ReadOnlySpan<byte> json, DateTimeOffset? now)
    {
        var reader = new Utf8JsonReader(json, isFinalBlock: true, state: default);
        var nowValue = now ?? DateTimeOffset.UtcNow;

        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
                throw new FormatException("SenML JSON must be an array");

            var results = new List<DecodedSenmlRecord>();
            string baseName = "";
            long baseTime = 0;
            bool hasBaseTime = false;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new FormatException("Expected object inside SenML array");

                string? localName = null;
                PayloadValue? value = null;
                long? localT = null;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                        throw new FormatException("Expected property name in SenML record");

                    var prop = reader.GetString();
                    if (!reader.Read())
                        throw new FormatException("Unexpected end of JSON in SenML record");

                    switch (prop)
                    {
                        case "bn":
                            baseName = reader.GetString() ?? "";
                            break;
                        case "bt":
                            baseTime = reader.GetInt64();
                            hasBaseTime = true;
                            break;
                        case "n":
                            localName = reader.GetString();
                            break;
                        case "t":
                            localT = reader.GetInt64();
                            break;
                        case "v":
                            // Prefer Integer fidelity when the JSON number is an integer literal
                            // (preserves full long precision and round-trips Integer-typed records).
                            // Falls back to Float for non-integer or out-of-long-range numbers.
                            if (reader.TryGetInt64(out long ivLong))
                                value = PayloadValue.FromInteger(ivLong);
                            else
                                value = PayloadValue.FromFloat(reader.GetDouble());
                            break;
                        case "vs":
                            value = PayloadValue.FromString(reader.GetString() ?? "");
                            break;
                        case "vb":
                            value = PayloadValue.FromBoolean(reader.GetBoolean());
                            break;
                        case "vd":
                            value = PayloadValue.FromOpaque(Convert.FromBase64String(reader.GetString() ?? ""));
                            break;
                        case "vlo":
                            value = PayloadValue.FromObjectLink(ObjectLink.Parse(reader.GetString() ?? ""));
                            break;
                        default:
                            // Unknown SenML field (e.g. bu, bver, s, ut): ignore but consume any nested structure.
                            reader.Skip();
                            break;
                    }
                }

                var resolvedName = baseName + (localName ?? "");
                var resolvedValue = value ?? PayloadValue.Empty;

                DateTimeOffset? timestamp = null;
                if (localT.HasValue && localT.Value >= SenmlAbsoluteTimeThreshold)
                {
                    // RFC 8428 §4.5.3: a per-record `t` that already encodes an absolute Unix
                    // timestamp is taken as-is. We deliberately do not also add `bt` here: stacking
                    // an absolute `bt` (e.g. 1.7e9) on top of an absolute `t` would shift the result
                    // far into the future, which is never a useful pack semantics.
                    timestamp = DateTimeOffset.FromUnixTimeSeconds(localT.Value);
                }
                else if (hasBaseTime || localT.HasValue)
                {
                    long effective = (hasBaseTime ? baseTime : 0L) + (localT ?? 0L);
                    timestamp = ResolveSenmlTime(effective, nowValue);
                }

                results.Add(new DecodedSenmlRecord(resolvedName, resolvedValue, timestamp));
            }

            return results;
        }
        catch (JsonException e)
        {
            throw new FormatException("Malformed SenML JSON pack: " + e.Message, e);
        }
        catch (FormatException)
        {
            throw;
        }
        catch (InvalidOperationException e)
        {
            // Utf8JsonReader throws InvalidOperationException when a reader accessor doesn't match
            // the current token type; surface it as FormatException for callers.
            throw new FormatException("Malformed SenML JSON pack: " + e.Message, e);
        }
    }

    private const long SenmlAbsoluteTimeThreshold = 1L << 28;  // RFC 8428, Section 4.5.3

    private static DateTimeOffset ResolveSenmlTime(long seconds, DateTimeOffset now)
    {
        // RFC 8428 §4.5.3: values strictly less than 2^28 are relative to "now"; values
        // greater than or equal to 2^28 are absolute Unix seconds.
        if (seconds < SenmlAbsoluteTimeThreshold)
            return now.AddSeconds(seconds);
        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }

    private static void WriteValue(Utf8JsonWriter writer, SenmlRecord record)
    {
        // RFC 8428, Section 4.4: select exactly one value field; OMA LwM2M adds "vlo" for object links.
        switch (record.Value.Type)
        {
            case PayloadType.Float:
                writer.WriteNumber("v", record.Value.AsDouble());
                break;
            case PayloadType.Integer:
                writer.WriteNumber("v", record.Value.AsLong());
                break;
            case PayloadType.String:
                writer.WriteString("vs", record.Value.AsString());
                break;
            case PayloadType.Boolean:
                writer.WriteBoolean("vb", record.Value.AsBoolean());
                break;
            case PayloadType.Opaque:
                writer.WriteString("vd", Convert.ToBase64String((byte[])record.Value.RawValue!));
                break;
            case PayloadType.Time:
                writer.WriteNumber("v", ((DateTimeOffset)record.Value.RawValue!).ToUnixTimeSeconds());
                break;
            case PayloadType.ObjectLink:
                writer.WriteString("vlo", record.Value.AsObjectLink().ToString());
                break;
            case PayloadType.None:
                // RFC 8428 §4.4 allows a record with no value field; emit name (and timestamp) only.
                break;
            default:
                throw new ArgumentException(
                    $"Unsupported payload type for SenML: {record.Value.Type}",
                    nameof(record));
        }
    }
}

/// <summary>
/// A SenML record to encode.
/// </summary>
/// <param name="ResourceId">The LwM2M resource ID, written as the SenML <c>n</c> field relative to <c>bn</c>.</param>
/// <param name="Value">The typed payload value carried by this record.</param>
/// <param name="Timestamp">
/// Optional sample time (RFC 8428, Section 4.5.3). Encoded as absolute Unix seconds in the <c>t</c>
/// field; no relative-time encoding is produced.
/// </param>
public sealed record SenmlRecord(
    int ResourceId,
    PayloadValue Value,
    DateTimeOffset? Timestamp = null);

/// <summary>
/// A decoded SenML record.
/// </summary>
/// <param name="Name">The fully resolved record name (<c>bn + n</c>), e.g. <c>/3303/0/5700</c>.</param>
/// <param name="Value">The typed payload value.</param>
/// <param name="Timestamp">
/// The resolved sample time, if the record (or any preceding base time) carried one.
/// Time resolution follows RFC 8428, Section 4.5.3 (relative or absolute based on magnitude).
/// </param>
public sealed record DecodedSenmlRecord(
    string Name,
    PayloadValue Value,
    DateTimeOffset? Timestamp);
