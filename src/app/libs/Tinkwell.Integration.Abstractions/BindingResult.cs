using Tinkwell.Coap;

namespace Tinkwell.Integration;

/// <summary>
/// Represents output produced by a binding. If a binding returns
/// <see langword="null"/> it means "no output" and the next binding
/// in the chain may produce one instead.
/// </summary>
/// <param name="Content">The response body bytes.</param>
/// <param name="ContentFormat">
/// CoAP Content-Format (e.g. <see cref="CoapContentFormat.TextPlain"/>,
/// <see cref="CoapContentFormat.ApplicationOctetStream"/>,
/// <see cref="CoapContentFormat.ApplicationJson"/>). Ignored by
/// protocols that do not support content negotiation.
/// </param>
public sealed record BindingResult(byte[] Content, CoapContentFormat ContentFormat);
