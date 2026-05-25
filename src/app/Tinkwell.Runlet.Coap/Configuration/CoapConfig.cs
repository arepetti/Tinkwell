namespace Tinkwell.Runlet.Coap.Configuration;

/// <summary>
/// Root configuration for all CoAP server definitions parsed from <c>.tw</c> files.
/// </summary>
public sealed record CoapConfig(IReadOnlyList<CoapServerDefinition> Servers);
