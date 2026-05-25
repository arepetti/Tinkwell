using Tinkwell.Coap;

namespace Tinkwell.Runlet.Coap;

/// <summary>
/// Represents the response produced by a CoAP middleware when it
/// short-circuits the pipeline or transforms the result.
/// </summary>
/// <param name="Content">Response body bytes.</param>
/// <param name="ContentFormat">CoAP Content-Format (RFC 7252).</param>
public sealed record CoapMiddlewareResult(byte[] Content, CoapContentFormat ContentFormat);
