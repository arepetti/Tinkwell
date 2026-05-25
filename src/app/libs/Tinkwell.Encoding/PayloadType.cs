namespace Tinkwell.Encoding;

/// <summary>
/// Data types that a protocol payload value can carry.
/// </summary>
/// <remarks>
/// Covers the primitive types used by LwM2M (OMA-TS-LightweightM2M_Core-V1_1,
/// Section 6.1, Table 7) and SenML (RFC 8428, Section 4.4).
/// </remarks>
public enum PayloadType
{
    /// <summary>UTF-8 text, encoded on the wire as bytes (TLV) or as a SenML <c>vs</c> field.</summary>
    String,

    /// <summary>Signed 64-bit integer, encoded on the wire in 1, 2, 4 or 8 big-endian bytes for TLV.</summary>
    Integer,

    /// <summary>IEEE 754 floating-point number, encoded as 4 (float) or 8 (double) big-endian bytes for TLV.</summary>
    Float,

    /// <summary>Boolean value, encoded as a single byte <c>0x00</c>/<c>0x01</c> for TLV or as a <c>vb</c> JSON field.</summary>
    Boolean,

    /// <summary>Arbitrary binary data, encoded verbatim for TLV and base64-encoded as a <c>vd</c> JSON field for SenML.</summary>
    Opaque,

    /// <summary>Absolute time, encoded as a Unix timestamp (seconds since 1970-01-01 UTC) for TLV.</summary>
    Time,

    /// <summary>
    /// LwM2M Object Link — a pair of 16-bit object/instance IDs identifying a target resource
    /// (OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3).
    /// </summary>
    /// <remarks>
    /// Known as <c>Objlnk</c> in OMA-TS terminology; renamed here for clarity. On the TLV wire format
    /// it occupies 4 bytes: 16-bit object ID followed by 16-bit instance ID, both big-endian. In the
    /// LwM2M SenML JSON profile it is carried as the <c>vlo</c> string field formatted as
    /// <c>"&lt;objectId&gt;:&lt;instanceId&gt;"</c>.
    /// </remarks>
    ObjectLink,

    /// <summary>
    /// Sentinel for an absent value (e.g. an empty TLV record or a SenML record with no value field).
    /// Distinct from any encoded value.
    /// </summary>
    None,
}
