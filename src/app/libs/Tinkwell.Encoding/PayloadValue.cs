namespace Tinkwell.Encoding;

/// <summary>
/// A typed value decoded from (or to be encoded into) a protocol payload.
/// </summary>
/// <remarks>
/// <para>
/// Carries one of the <see cref="PayloadType"/> primitives: string, integer, float, boolean,
/// opaque bytes, timestamp, or object-link. Instances are immutable; the type and the underlying
/// representation are fixed at construction.
/// </para>
/// <para>
/// Conversion accessors (<see cref="AsDouble"/>, <see cref="AsLong"/>, <see cref="AsBoolean"/>,
/// <see cref="AsObjectLink"/>) only convert between numerically compatible types; they throw
/// <see cref="InvalidOperationException"/> when the request is incompatible with <see cref="Type"/>.
/// <see cref="AsString"/> is total: it returns the empty string for <see cref="PayloadValue.Empty"/>.
/// </para>
/// </remarks>
public sealed record PayloadValue
{
    /// <summary>The kind of value carried by this instance.</summary>
    public PayloadType Type { get; }

    /// <summary>
    /// The underlying CLR value (<see cref="string"/>, <see cref="long"/>, <see cref="double"/>,
    /// <see cref="bool"/>, <see cref="byte"/>[], <see cref="DateTimeOffset"/>, <see cref="ObjectLink"/>,
    /// or <see langword="null"/> for <see cref="Empty"/>). Use the typed <c>As*</c> accessors instead
    /// when possible.
    /// </summary>
    public object? RawValue { get; }

    private PayloadValue(PayloadType type, object? value)
    {
        Type = type;
        RawValue = value;
    }

    /// <summary>Creates a string-valued payload.</summary>
    public static PayloadValue FromString(string value) => new(PayloadType.String, value);

    /// <summary>Creates a signed 64-bit integer payload.</summary>
    public static PayloadValue FromInteger(long value) => new(PayloadType.Integer, value);

    /// <summary>Creates a 64-bit floating-point payload.</summary>
    public static PayloadValue FromFloat(double value) => new(PayloadType.Float, value);

    /// <summary>Creates a boolean payload.</summary>
    public static PayloadValue FromBoolean(bool value) => new(PayloadType.Boolean, value);

    /// <summary>
    /// Creates an opaque (binary) payload. The provided array is referenced directly and is
    /// expected to be treated as immutable by the caller.
    /// </summary>
    /// <param name="value">Raw bytes to wrap. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static PayloadValue FromOpaque(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(PayloadType.Opaque, value);
    }

    /// <summary>Creates a timestamp payload (absolute time, UTC-comparable).</summary>
    public static PayloadValue FromTime(DateTimeOffset value) => new(PayloadType.Time, value);

    /// <summary>
    /// Creates an LwM2M object-link payload pointing at a specific object instance.
    /// </summary>
    /// <param name="objectId">The 16-bit LwM2M object ID (0 to 65535).</param>
    /// <param name="instanceId">The 16-bit LwM2M object instance ID (0 to 65535).</param>
    /// <exception cref="ArgumentOutOfRangeException">An ID is outside the unsigned 16-bit range.</exception>
    public static PayloadValue FromObjectLink(int objectId, int instanceId)
        => FromObjectLink(new ObjectLink(objectId, instanceId));

    /// <summary>Creates an LwM2M object-link payload from an <see cref="Encoding.ObjectLink"/> value.</summary>
    public static PayloadValue FromObjectLink(ObjectLink value) => new(PayloadType.ObjectLink, value);

    /// <summary>The canonical empty payload (<see cref="PayloadType.None"/>, no underlying value).</summary>
    public static readonly PayloadValue Empty = new(PayloadType.None, null);

    /// <summary>
    /// Returns the value as a <see cref="double"/>. Integers are widened; other types throw.
    /// </summary>
    /// <exception cref="InvalidOperationException">The underlying type cannot be converted to <see cref="double"/>.</exception>
    public double AsDouble() => Type switch
    {
        PayloadType.Float => (double)RawValue!,
        PayloadType.Integer => (long)RawValue!,
        _ => throw new InvalidOperationException($"Cannot convert {Type} to double"),
    };

    /// <summary>
    /// Returns the value as a <see cref="long"/>. Floating-point values are truncated toward zero;
    /// other non-numeric types throw.
    /// </summary>
    /// <exception cref="InvalidOperationException">The underlying type cannot be converted to <see cref="long"/>.</exception>
    public long AsLong() => Type switch
    {
        PayloadType.Integer => (long)RawValue!,
        PayloadType.Float => (long)(double)RawValue!,
        _ => throw new InvalidOperationException($"Cannot convert {Type} to long"),
    };

    /// <summary>
    /// Returns a string representation of the value. Returns the empty string for <see cref="Empty"/>;
    /// for non-string types this is the result of <see cref="object.ToString"/> on the raw value
    /// (intended for diagnostics, not for round-tripping).
    /// </summary>
    public string AsString() => RawValue?.ToString() ?? "";

    /// <summary>
    /// Returns the value as a <see cref="bool"/>. Integers convert with the usual zero/non-zero rule;
    /// other types throw.
    /// </summary>
    /// <exception cref="InvalidOperationException">The underlying type cannot be converted to <see cref="bool"/>.</exception>
    public bool AsBoolean() => Type switch
    {
        PayloadType.Boolean => (bool)RawValue!,
        PayloadType.Integer => (long)RawValue! != 0,
        _ => throw new InvalidOperationException($"Cannot convert {Type} to boolean"),
    };

    /// <summary>
    /// Returns the value as an <see cref="Encoding.ObjectLink"/>. Throws if the payload type is
    /// not <see cref="PayloadType.ObjectLink"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The underlying type is not <see cref="PayloadType.ObjectLink"/>.</exception>
    public ObjectLink AsObjectLink() => Type switch
    {
        PayloadType.ObjectLink => (ObjectLink)RawValue!,
        _ => throw new InvalidOperationException($"Cannot convert {Type} to ObjectLink"),
    };

    /// <summary>
    /// Returns the value as a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For <see cref="PayloadType.Time"/> the stored timestamp is returned directly. For
    /// <see cref="PayloadType.Integer"/> the value is interpreted as Unix seconds (via
    /// <see cref="DateTimeOffset.FromUnixTimeSeconds(long)"/>); this is the ergonomic counterpart
    /// of decoding a SenML <c>v</c> field that originally encoded a <see cref="PayloadType.Time"/>
    /// resource (see <see cref="SenmlJsonCodec"/> remarks). Other types throw.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">The underlying type cannot be converted to <see cref="DateTimeOffset"/>.</exception>
    public DateTimeOffset AsTime() => Type switch
    {
        PayloadType.Time => (DateTimeOffset)RawValue!,
        PayloadType.Integer => DateTimeOffset.FromUnixTimeSeconds((long)RawValue!),
        _ => throw new InvalidOperationException($"Cannot convert {Type} to DateTimeOffset"),
    };
}

/// <summary>
/// An LwM2M object link: a pair of 16-bit identifiers naming an object and one of its instances
/// (OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3, "Objlnk").
/// </summary>
/// <remarks>
/// On the TLV wire it is encoded as four big-endian bytes: object ID then instance ID. The
/// special pair <c>(65535, 65535)</c> denotes the OMA "null link" (no target).
/// </remarks>
public readonly record struct ObjectLink
{
    /// <summary>The 16-bit LwM2M object ID.</summary>
    public int ObjectId { get; }

    /// <summary>The 16-bit LwM2M object instance ID.</summary>
    public int InstanceId { get; }

    /// <summary>Initializes a new <see cref="ObjectLink"/> with the given object/instance IDs.</summary>
    /// <param name="objectId">The 16-bit LwM2M object ID (0 to 65535).</param>
    /// <param name="instanceId">The 16-bit LwM2M object instance ID (0 to 65535).</param>
    /// <exception cref="ArgumentOutOfRangeException">An ID is outside the unsigned 16-bit range.</exception>
    public ObjectLink(int objectId, int instanceId)
    {
        if (objectId is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(objectId), objectId, "Object ID must fit in 16 unsigned bits (0..65535).");
        if (instanceId is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(instanceId), instanceId, "Instance ID must fit in 16 unsigned bits (0..65535).");

        ObjectId = objectId;
        InstanceId = instanceId;
    }

    /// <summary>The OMA "null link" value (<c>65535:65535</c>) indicating the link is not bound.</summary>
    public static ObjectLink Null { get; } = new(ushort.MaxValue, ushort.MaxValue);

    /// <summary>Returns <see langword="true"/> if this link is the <see cref="Null"/> sentinel.</summary>
    public bool IsNull => ObjectId == ushort.MaxValue && InstanceId == ushort.MaxValue;

    /// <summary>
    /// Returns the canonical OMA SenML JSON string form of this link, <c>"&lt;objectId&gt;:&lt;instanceId&gt;"</c>.
    /// </summary>
    public override string ToString() => $"{ObjectId}:{InstanceId}";

    /// <summary>
    /// Parses an object link in OMA SenML JSON string form, <c>"&lt;objectId&gt;:&lt;instanceId&gt;"</c>.
    /// </summary>
    /// <exception cref="FormatException">The input is not a valid object-link string.</exception>
    public static ObjectLink Parse(string value)
    {
        if (TryParse(value, out var link))
            return link;
        throw new FormatException($"Invalid object-link value: '{value}' (expected '<objectId>:<instanceId>').");
    }

    /// <summary>
    /// Tries to parse an object link in OMA SenML JSON string form, <c>"&lt;objectId&gt;:&lt;instanceId&gt;"</c>.
    /// Surrounding whitespace is tolerated; whitespace inside the two segments is not.
    /// </summary>
    public static bool TryParse(string? value, out ObjectLink result)
    {
        result = default;
        if (value is null)
            return false;

        var span = value.AsSpan().Trim();
        int colon = span.IndexOf(':');
        if (colon <= 0 || colon == span.Length - 1)
            return false;

        if (!int.TryParse(span[..colon], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var objectId))
            return false;
        if (!int.TryParse(span[(colon + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var instanceId))
            return false;

        if (objectId is < 0 or > ushort.MaxValue || instanceId is < 0 or > ushort.MaxValue)
            return false;

        result = new ObjectLink(objectId, instanceId);
        return true;
    }
}
