using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.ProtobufGateway.Configuration;

/// <summary>
/// A single <c>protobuf-gateway</c> block parsed from the <c>.tw</c>
/// configuration file. Defines an access profile that attaches to a
/// protobuf gateway runlet by name.
/// </summary>
/// <param name="Name">Profile label (the block name), used for logging and future identity mapping.</param>
/// <param name="Target">
/// Runlet name to attach to (from the <c>for</c> modifier).
/// Defaults to <c>"*"</c> (matches any protobuf gateway runlet).
/// </param>
/// <param name="MatchPattern">
/// Path template for incoming CoAP requests (from the <c>match</c> modifier).
/// Must contain <c>{service}</c> and <c>{method}</c> placeholders.
/// Defaults to <c>"/{service}/{method}"</c>.
/// </param>
/// <param name="AllowRules">Service patterns that this profile permits.</param>
/// <param name="Location">Source location for diagnostics.</param>
public sealed record GatewayProfileConfig(
    string Name,
    string Target,
    string MatchPattern,
    IReadOnlyList<AllowRuleConfig> AllowRules,
    SourceLocation Location);
