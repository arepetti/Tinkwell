using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Mqtt.Configuration;

/// <summary>
/// A topic subscription parsed from a <c>subscribe</c> child block
/// inside an <c>mqtt</c> connection block. Each subscription must contain
/// at least one <c>on message</c> block with bindings; event publishing
/// and other side-effects are performed by those bindings.
/// </summary>
/// <param name="TopicFilter">MQTT topic filter (e.g. <c>"sensor/+"</c>).</param>
/// <param name="VerbBlocks">Required list of <c>on message</c> blocks with bindings.</param>
/// <param name="Location">Source location for diagnostics.</param>
public sealed record MqttSubscriptionDefinition(
    string TopicFilter,
    IReadOnlyList<MqttVerbBlock> VerbBlocks,
    SourceLocation Location);
