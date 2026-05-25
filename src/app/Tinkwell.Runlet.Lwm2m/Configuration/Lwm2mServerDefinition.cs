using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Lwm2m.Configuration;

/// <summary>
/// A named LwM2M server that listens on a UDP port, processing
/// registration, read, write, and observation requests.
/// </summary>
public sealed record Lwm2mServerDefinition(
    string Name,
    int Port,
    IReadOnlyList<Lwm2mObjectMapping> Objects,
    Lwm2mRegistrationOptions Registration,
    SourceLocation Location);

/// <summary>
/// Maps an LwM2M object/resource to a Tinkwell measure, controlling
/// how values flow in from devices.
/// </summary>
public sealed record Lwm2mObjectMapping(
    int ObjectId,
    int ResourceId,
    string MeasureName,
    bool Observable,
    SourceLocation Location);

/// <summary>
/// Registration behavior options.
/// </summary>
public sealed record Lwm2mRegistrationOptions
{
    /// <summary>
    /// Default lifetime in seconds for client registrations when the client
    /// does not specify one (OMA-TS-LightweightM2M_Core-V1_1, Section 5.3, Table 6.1).
    /// </summary>
    public int DefaultLifetimeSeconds { get; init; } = 86400;

    /// <summary>
    /// Whether to emit a Tinkwell event when a client registers or deregisters.
    /// </summary>
    public bool EmitEvents { get; init; } = true;
}
