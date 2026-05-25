using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Coap.Configuration;

/// <summary>
/// A CoAP resource path pattern with per-verb binding groups.
/// </summary>
/// <param name="PathPattern">
/// URI path pattern (e.g. <c>/sensor/+</c>). <c>+</c> matches a single
/// segment; <c>#</c> matches zero or more trailing segments.
/// </param>
/// <param name="VerbBlocks">
/// Ordered list of <c>on &lt;verb&gt;</c> blocks. Multiple blocks for the
/// same verb are allowed (all matching blocks execute in order).
/// </param>
public sealed record CoapResourceDefinition(
    string PathPattern,
    IReadOnlyList<CoapVerbBlock> VerbBlocks,
    SourceLocation Location);
