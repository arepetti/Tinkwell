namespace Tinkwell.Runner;

/// <summary>
/// Describes a service registered by a runlet, containing the metadata
/// required for service discovery. Produced by
/// <see cref="IGrpcEndpointMapper.MapService{TService}()"/> and reported
/// to the coordinator for centralized lookup.
/// </summary>
/// <param name="Name">
/// The canonical service name. For gRPC services this is the protobuf
/// fully-qualified name (e.g. <c>tinkwell.sensors.TemperatureReader</c>).
/// </param>
/// <param name="Type">
/// The transport protocol of this service (gRPC, API, etc.).
/// </param>
/// <param name="FriendlyName">
/// An optional human-readable display name.
/// </param>
/// <param name="FamilyName">
/// An optional group name for logically related services
/// (e.g. <c>sensors</c>). Useful for bulk discovery queries.
/// </param>
/// <param name="Aliases">
/// Alternative names under which this service can be discovered.
/// </param>
/// <param name="Host">
/// The network host in <c>ip:port</c> form (e.g. <c>127.0.0.1:4900</c>).
/// </param>
/// <param name="Url">
/// The full URL clients should use to reach the service
/// (e.g. <c>http://127.0.0.1:4900/tinkwell.sensors.TemperatureReader</c>).
/// </param>
public sealed record ServiceDefinition(
    string Name,
    ServiceType Type,
    string? FriendlyName,
    string? FamilyName,
    IReadOnlyList<string> Aliases,
    string Host,
    string Url);
