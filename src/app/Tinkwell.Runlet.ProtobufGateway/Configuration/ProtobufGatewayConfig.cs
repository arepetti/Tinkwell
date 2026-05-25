namespace Tinkwell.Runlet.ProtobufGateway.Configuration;

/// <summary>
/// Root configuration containing all <c>protobuf-gateway</c> blocks.
/// </summary>
public sealed record ProtobufGatewayConfig(
    IReadOnlyList<GatewayProfileConfig> Gateways);
