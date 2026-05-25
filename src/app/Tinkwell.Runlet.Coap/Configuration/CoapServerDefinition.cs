using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Coap.Configuration;

/// <summary>
/// A named CoAP server that listens on a UDP port.
/// </summary>
public sealed record CoapServerDefinition(
    string Name,
    int Port,
    int MaxConcurrentRequests,
    int MaxPendingRequests,
    IReadOnlyList<CoapResourceDefinition> Resources,
    SourceLocation Location);
