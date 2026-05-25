namespace Tinkwell.Integration;

/// <summary>
/// Implemented by runlets that want to expose custom LwM2M object resources.
/// Register in DI during <c>ConfigureServices</c>; the LwM2M runlet discovers
/// all providers at startup and maps their resources alongside
/// <c>.tw</c>-configured object mappings.
/// </summary>
public interface ILwm2mResourceProvider
{
    /// <summary>
    /// Returns the set of LwM2M resources this provider handles.
    /// Called once at startup.
    /// </summary>
    IReadOnlyList<Lwm2mResourceRegistration> GetResources();
}

/// <summary>
/// Registers a handler for a single LwM2M object/resource pair.
/// Values are exchanged as strings to avoid a dependency on
/// <c>Tinkwell.Encoding</c>; the LwM2M runlet handles format
/// negotiation (TLV, SenML, text/plain) transparently.
/// </summary>
/// <param name="ObjectId">OMA object identifier (e.g. 3303 for Temperature).</param>
/// <param name="ResourceId">Resource identifier within the object (e.g. 5700 for Sensor Value).</param>
/// <param name="OnRead">
/// Returns the current value as a string, or <see langword="null"/> if unavailable.
/// Called on CoAP GET for this resource path.
/// </param>
/// <param name="OnWrite">
/// Receives the written value as a string. May be <see langword="null"/>
/// for read-only resources. Called on CoAP PUT/POST for this resource path.
/// </param>
public sealed record Lwm2mResourceRegistration(
    int ObjectId,
    int ResourceId,
    Func<string?> OnRead,
    Action<string>? OnWrite = null);
