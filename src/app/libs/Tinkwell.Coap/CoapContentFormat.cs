namespace Tinkwell.Coap;

/// <summary>
/// Well-known CoAP Content-Format identifiers, used in the Content-Format option
/// (RFC 7252, Section 5.10.3) and the Accept option (RFC 7252, Section 5.10.4).
/// </summary>
/// <remarks>
/// <para>
/// Values are non-negative integers from the IANA "CoAP Content-Formats" registry
/// (<see href="https://www.iana.org/assignments/core-parameters/core-parameters.xhtml"/>).
/// Only formats actually used by this library and its callers are enumerated here;
/// cast an <see cref="int"/> directly to this type for unlisted values.
/// </para>
/// <para>
/// The underlying numeric value is the on-wire identifier. Ranges are assigned per media-type
/// family: 0-255 for expert review, 256-9999 for IETF review, 10000-64999 for first-come
/// first-served (OMA uses this range for LwM2M), 65000-65535 for experimental use.
/// </para>
/// </remarks>
public enum CoapContentFormat
{
    /// <summary>RFC 7252, Section 12.3 - <c>text/plain; charset=utf-8</c>.</summary>
    TextPlain = 0,

    /// <summary>RFC 6690, Section 7.2 - <c>application/link-format</c>, used by CoRE resource discovery.</summary>
    ApplicationLinkFormat = 40,

    /// <summary>RFC 7252, Section 12.3 - <c>application/octet-stream</c>, for opaque binary payloads.</summary>
    ApplicationOctetStream = 42,

    /// <summary>RFC 7252, Section 12.3 - <c>application/json</c>.</summary>
    ApplicationJson = 50,

    /// <summary>RFC 7049 - <c>application/cbor</c> (Concise Binary Object Representation).</summary>
    ApplicationCbor = 60,

    /// <summary>RFC 8428, Section 12.3 - <c>application/senml+json</c> (SenML JSON).</summary>
    ApplicationSenmlJson = 110,

    /// <summary>RFC 8428, Section 12.3 - <c>application/senml+cbor</c> (SenML CBOR).</summary>
    ApplicationSenmlCbor = 112,

    /// <summary>
    /// OMA LwM2M TLV - <c>application/vnd.oma.lwm2m+tlv</c>
    /// (OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3).
    /// </summary>
    ApplicationLwm2mTlv = 11542,

    /// <summary>
    /// OMA LwM2M JSON - <c>application/vnd.oma.lwm2m+json</c>
    /// (OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.4).
    /// </summary>
    ApplicationLwm2mJson = 11543,
}
