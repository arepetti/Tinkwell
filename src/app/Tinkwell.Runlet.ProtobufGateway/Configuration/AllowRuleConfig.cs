using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.ProtobufGateway.Configuration;

/// <summary>
/// A single <c>allow</c> entry in a <c>protobuf-gateway</c> block.
/// The <see cref="ServicePattern"/> is a glob-style pattern matched
/// against the full proto service name (e.g. <c>"tinkwell.measures.*"</c>).
/// </summary>
public sealed record AllowRuleConfig(string ServicePattern, SourceLocation Location);
