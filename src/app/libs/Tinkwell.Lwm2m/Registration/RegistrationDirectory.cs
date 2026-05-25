using System.Collections.Concurrent;

namespace Tinkwell.Lwm2m.Registration;

/// <summary>
/// Manages the lifecycle of LwM2M client registrations
/// (OMA-TS-LightweightM2M_Transport-V1_1, Section 5.3).
/// Thread-safe for concurrent register, update, and deregister operations.
/// <see cref="PurgeExpired"/> is safe for typical use; concurrent registration changes
/// for the same endpoint while a purge is running may briefly observe transient inconsistency
/// (e.g. a location entry removed and not restored if a newer registration has replaced the endpoint).
/// </summary>
public sealed class RegistrationDirectory
{
    private readonly ConcurrentDictionary<string, Lwm2mRegistration> _byLocation = new();
    private readonly ConcurrentDictionary<string, Lwm2mRegistration> _byEndpoint = new();
    private int _nextLocationId;

    /// <summary>
    /// Registers a new client (Section 5.3.1). If the endpoint is already
    /// registered, the old registration is replaced (implicit re-register).
    /// Returns the server-assigned location path.
    /// </summary>
    /// <example>
    /// <para>Build a <see cref="Lwm2mRegistration"/> (e.g. with <see cref="RegistrationParser"/>) and assign a server <c>/rd/n</c> <see cref="Lwm2mRegistration.Location"/>.</para>
    /// <code language="csharp">
    /// var directory = new RegistrationDirectory();
    /// var clientEp = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 5683);
    /// var payload = Tinkwell.Lwm2m.LinkFormatBuilder.BuildRegistrationPayload(new[] { "3/0", "3303/0" });
    /// var fromClient = RegistrationParser.Parse("ep=gw-1&amp;lt=300", payload, clientEp);
    /// var onServer = directory.Register(fromClient);
    /// // onServer.Location is e.g. "/rd/1"
    /// </code>
    /// </example>
    /// <param name="registration">Registration data parsed from the client's <c>POST /rd</c> (typically via <see cref="RegistrationParser.Parse"/>). The <see cref="Lwm2mRegistration.Location"/> field is overwritten by the server-assigned value.</param>
    public Lwm2mRegistration Register(Lwm2mRegistration registration)
    {
        if (_byEndpoint.TryRemove(registration.Endpoint, out var existing))
            _byLocation.TryRemove(existing.Location, out _);

        var location = $"/rd/{Interlocked.Increment(ref _nextLocationId)}";
        var registered = registration with { Location = location };

        _byLocation[location] = registered;
        _byEndpoint[registered.Endpoint] = registered;
        return registered;
    }

    /// <summary>
    /// Updates an existing registration's lifetime (Section 5.3.2).
    /// Returns false if the location is not found.
    /// </summary>
    /// <example>
    /// <para>Refresh the registration timer and keep the same <c>Location</c> (Section 5.3.2).</para>
    /// <code language="csharp">
    /// Lwm2mRegistration onServer = directory.Register(fromClient);
    /// bool ok = directory.Update(onServer.Location, newLifetime: 600);
    /// </code>
    /// </example>
    /// <param name="location">Server-assigned registration path (e.g. <c>"/rd/1"</c>) returned by <see cref="Register"/>.</param>
    /// <param name="newLifetime">New lifetime in seconds, or <c>null</c> to keep the existing value. The <see cref="Lwm2mRegistration.RegisteredAt"/> timestamp is always refreshed.</param>
    public bool Update(string location, int? newLifetime = null)
    {
        if (!_byLocation.TryGetValue(location, out var existing))
            return false;

        var updated = existing with
        {
            RegisteredAt = DateTimeOffset.UtcNow,
            Lifetime = newLifetime ?? existing.Lifetime,
        };

        _byLocation[location] = updated;
        _byEndpoint[updated.Endpoint] = updated;
        return true;
    }

    /// <summary>
    /// Removes a client registration (Section 5.3.4).
    /// </summary>
    /// <example>
    /// <para>Delete the registration for a CoAP deregister (Section 5.3.4) using the <c>/rd/…</c> path the server returned when registering.</para>
    /// <code language="csharp">
    /// var onServer = directory.Register(fromClient);
    /// bool removed = directory.Deregister(onServer.Location);
    /// // Endpoint is gone from the directory; a new register yields a new /rd/n.
    /// </code>
    /// </example>
    /// <param name="location">Server-assigned registration path (e.g. <c>"/rd/1"</c>) to remove.</param>
    public bool Deregister(string location)
    {
        if (!_byLocation.TryRemove(location, out var removed))
            return false;
        _byEndpoint.TryRemove(removed.Endpoint, out _);
        return true;
    }

    /// <summary>
    /// Returns the registration for a server location path, or null if not found.
    /// </summary>
    /// <example>
    /// <para>Resolve a client by the server-assigned path (e.g. when handling requests to that registration resource).</para>
    /// <code language="csharp">
    /// var onServer = directory.Register(fromClient);
    /// string path = onServer.Location;
    /// Lwm2mRegistration? r = directory.FindByLocation(path);
    /// // r is the same record as onServer for that /rd/…
    /// </code>
    /// </example>
    /// <param name="location">Server-assigned registration path (e.g. <c>"/rd/1"</c>).</param>
    public Lwm2mRegistration? FindByLocation(string location) =>
        _byLocation.GetValueOrDefault(location);

    /// <summary>
    /// Returns the registration for a client endpoint name, or null if not found.
    /// </summary>
    /// <example>
    /// <para>Map the LwM2M <c>ep=</c> name to the current registration, including the server <c>Location</c>, after <see cref="Register"/>.</para>
    /// <code language="csharp">
    /// directory.Register(RegistrationParser.Parse("ep=room-a&amp;lt=300", payload, clientEp));
    /// Lwm2mRegistration? r = directory.FindByEndpoint("room-a");
    /// if (r is not null) { string loc = r.Location; /* e.g. "/rd/2" */ }
    /// </code>
    /// </example>
    /// <param name="endpoint">Client endpoint name (the <c>ep=</c> value from the registration query).</param>
    public Lwm2mRegistration? FindByEndpoint(string endpoint) =>
        _byEndpoint.GetValueOrDefault(endpoint);

    /// <summary>
    /// Snapshot of all registrations by server-assigned location. The collection is
    /// materialized; callers should not assume it stays in sync with concurrent updates.
    /// </summary>
    public IReadOnlyCollection<Lwm2mRegistration> All => _byLocation.Values.ToList();

    /// <summary>
    /// Removes all expired registrations. Returns the number removed.
    /// </summary>
    /// <example>
    /// <para>Run on a timer to remove registrations past <see cref="Lwm2mRegistration.ExpiresAt"/> (no live <see cref="Update"/>).</para>
    /// <code language="csharp">
    /// int removedCount = directory.PurgeExpired();
    /// </code>
    /// </example>
    public int PurgeExpired()
    {
        int count = 0;
        foreach (var kvp in _byLocation)
        {
            if (!kvp.Value.IsExpired)
                continue;

            if (_byLocation.TryRemove(kvp.Key, out var removed))
            {
                if (!removed.IsExpired)
                {
                    // Concurrent Update may have refreshed this location after our snapshot; restore.
                    if (!_byEndpoint.TryGetValue(removed.Endpoint, out var current) ||
                        current.Location == removed.Location)
                    {
                        _byLocation[removed.Location] = removed;
                        _byEndpoint[removed.Endpoint] = removed;
                    }
                }
                else
                {
                    if (_byEndpoint.TryGetValue(removed.Endpoint, out var ep) &&
                        ep.Location == removed.Location)
                    {
                        _byEndpoint.TryRemove(removed.Endpoint, out _);
                    }
                    count++;
                }
            }
        }
        return count;
    }
}
