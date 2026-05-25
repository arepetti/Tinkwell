using System.Net;

namespace Tinkwell.Lwm2m.Registration;

/// <summary>
/// Represents a client registration at the LwM2M server
/// (OMA-TS-LightweightM2M_Transport-V1_1, Section 5.3.1).
/// </summary>
public sealed record Lwm2mRegistration
{
    /// <summary>Client-chosen LwM2M endpoint name (uniquely identifies the client on this server).</summary>
    public required string Endpoint { get; init; }
    /// <summary>Remote transport address the client used when registering (e.g. UDP source).</summary>
    public required IPEndPoint Address { get; init; }
    /// <summary>UTC time when the registration (or last successful <c>Update</c>) was applied.</summary>
    public required DateTimeOffset RegisteredAt { get; init; }
    /// <summary>Registration lifetime in seconds (as in the lwm2m or lifetime parameter per transport spec).</summary>
    public required int Lifetime { get; init; }
    /// <summary>Protocol version string from the client, if provided (e.g. <c>1.1</c>).</summary>
    public string? LwM2MVersion { get; init; }
    /// <summary>Client binding / queue mode (e.g. <c>U</c>, <c>UQ</c>, <c>UDP</c> depending on use).</summary>
    public string? BindingMode { get; init; }

    /// <summary>
    /// Object-instance pairs the client declared in the registration
    /// (OMA-TS-LightweightM2M_Transport-V1_1, Section 5.3.1, Table 6.1).
    /// </summary>
    public IReadOnlyList<Lwm2mPath> Objects { get; init; } = [];

    /// <summary>Time at which the registration is considered expired if not updated (<see cref="RegisteredAt"/> + <see cref="Lifetime"/>).</summary>
    public DateTimeOffset ExpiresAt => RegisteredAt.AddSeconds(Lifetime);
    /// <summary>True when the current time is after <see cref="ExpiresAt"/>.</summary>
    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;

    /// <summary>
    /// Registration location path (server-assigned), used as the key in
    /// the registration directory.
    /// </summary>
    public required string Location { get; init; }
}
