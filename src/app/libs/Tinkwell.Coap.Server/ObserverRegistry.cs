using System.Collections.Concurrent;
using System.Net;

namespace Tinkwell.Coap.Server;

/// <summary>
/// Tracks CoAP Observe registrations (RFC 7641, Section 3).
/// </summary>
/// <remarks>
/// <para>
/// Each observer is identified by the pair <c>(remote endpoint, token)</c> and watches exactly
/// one resource path. The registry is thread-safe for concurrent registration, deregistration,
/// and lookup and is used internally by <see cref="CoapServer"/>; applications normally interact
/// with it only for diagnostics or to force-remove observers after an authentication failure.
/// </para>
/// <para>
/// Tokens supplied to <see cref="Register(IPEndPoint, byte[], string)"/> and
/// <see cref="Deregister(IPEndPoint, byte[])"/> are defensively copied, so callers may reuse
/// their <c>byte[]</c> buffers after the call returns.
/// </para>
/// </remarks>
public sealed class ObserverRegistry
{
    private readonly ConcurrentDictionary<ObserverKey, ObserverEntry> _observers = new();

    /// <summary>
    /// Registers an observer for a resource path (RFC 7641, Section 3.1).
    /// </summary>
    /// <param name="remoteEndpoint">Remote endpoint that sent the Observe request.</param>
    /// <param name="token">CoAP token chosen by the client (0-8 bytes). Defensively copied.</param>
    /// <param name="path">Resource path the observer is watching (e.g. <c>"/3303/0/5700"</c>).</param>
    /// <remarks>
    /// If the same <c>(endpoint, token)</c> pair is already registered for a different path the
    /// previous registration is replaced.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="remoteEndpoint"/>, <paramref name="token"/> or <paramref name="path"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <para>Called by the server when a client registers Observe; application code can call it only in advanced tests or mocks.</para>
    /// <code>
    /// registry.Register(client, tokenBytes, "/3303/0/5700");
    /// </code>
    /// </example>
    public void Register(IPEndPoint remoteEndpoint, byte[] token, string path)
    {
        ArgumentNullException.ThrowIfNull(remoteEndpoint);
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(path);

        var tokenCopy = (byte[])token.Clone();
        var key = new ObserverKey(remoteEndpoint, tokenCopy);
        var entry = new ObserverEntry(path, remoteEndpoint, tokenCopy);
        _observers.AddOrUpdate(key, entry, (_, _) => entry);
    }

    /// <summary>
    /// Removes an observer (RFC 7641, Section 3.6).
    /// </summary>
    /// <param name="remoteEndpoint">Remote endpoint to remove.</param>
    /// <param name="token">Token originally supplied at registration.</param>
    /// <returns><see langword="true"/> if an observer was removed, <see langword="false"/> if none matched.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="remoteEndpoint"/> or <paramref name="token"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <para>End an Observe relation from application code, for example when the client sends RST and you mirror it in tests.</para>
    /// <code>
    /// var removed = registry.Deregister(remote, token);
    /// </code>
    /// </example>
    public bool Deregister(IPEndPoint remoteEndpoint, byte[] token)
    {
        ArgumentNullException.ThrowIfNull(remoteEndpoint);
        ArgumentNullException.ThrowIfNull(token);

        var key = new ObserverKey(remoteEndpoint, token);
        return _observers.TryRemove(key, out _);
    }

    /// <summary>
    /// Returns all observers currently watching the given resource path.
    /// </summary>
    /// <param name="path">Resource path to look up.</param>
    /// <returns>A snapshot of observers at the time of the call; never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <para>Diagnostics: list every client watching a LwM2m object before forcing a deregister.</para>
    /// <code>
    /// foreach (var o in registry.GetObservers("/3/0/9"))
    ///     logger.LogInformation("Observer at {EndPoint}", o.RemoteEndpoint);
    /// </code>
    /// </example>
    public IReadOnlyList<ObserverEntry> GetObservers(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Hot path: avoid the LINQ pipeline / iterator allocations on every notification.
        // A list is still allocated to return a stable snapshot, but we pre-size it to the
        // full registry as an upper bound and skip the enumerator-wrapping overhead.
        List<ObserverEntry>? matches = null;
        foreach (var entry in _observers.Values)
        {
            if (string.Equals(entry.Path, path, StringComparison.Ordinal))
            {
                matches ??= new List<ObserverEntry>(capacity: 4);
                matches.Add(entry);
            }
        }
        return matches ?? (IReadOnlyList<ObserverEntry>)Array.Empty<ObserverEntry>();
    }

    /// <summary>
    /// Removes all observers associated with a specific remote endpoint.
    /// </summary>
    /// <param name="remoteEndpoint">Endpoint whose observers should be removed.</param>
    /// <returns>The number of observers removed.</returns>
    /// <remarks>
    /// Useful when an authentication/authorization state change invalidates every subscription
    /// from a given client.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="remoteEndpoint"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <para>After the session is revoked, drop every Observe subscription from that client.</para>
    /// <code>
    /// int n = server.Observers.RemoveAll(suspiciousClient);
    /// </code>
    /// </example>
    public int RemoveAll(IPEndPoint remoteEndpoint)
    {
        ArgumentNullException.ThrowIfNull(remoteEndpoint);

        var toRemove = _observers
            .Where(kv => kv.Key.RemoteEndpoint.Equals(remoteEndpoint))
            .Select(kv => kv.Key)
            .ToList();

        int removed = 0;
        foreach (var key in toRemove)
        {
            if (_observers.TryRemove(key, out _))
                removed++;
        }
        return removed;
    }

    /// <summary>Number of active observers across all resources and clients.</summary>
    public int Count => _observers.Count;
}

/// <summary>
/// An active Observe subscription for a single <c>(client, resource)</c> pair (RFC 7641, Section 3.3).
/// </summary>
/// <remarks>
/// Instances are created by <see cref="ObserverRegistry"/>; application code should treat them as
/// opaque bookkeeping records. The embedded token is a defensive copy of the client's token.
/// </remarks>
public sealed class ObserverEntry
{
    private readonly byte[] _token;
    private int _sequenceNumber;

    /// <summary>Path the observer is watching, as supplied at registration time.</summary>
    public string Path { get; }

    /// <summary>Remote endpoint of the observer.</summary>
    public IPEndPoint RemoteEndpoint { get; }

    /// <summary>
    /// The CoAP token that correlates notifications with the original Observe request
    /// (RFC 7252, Section 5.3.1).
    /// </summary>
    /// <remarks>
    /// Exposed as <see cref="ReadOnlyMemory{T}"/> to prevent accidental mutation of the registry
    /// key. The underlying buffer is owned by the registry and must not be modified.
    /// </remarks>
    public ReadOnlyMemory<byte> Token => _token;

    internal byte[] TokenBytes => _token;

    internal ObserverEntry(string path, IPEndPoint remoteEndpoint, byte[] token)
    {
        Path = path;
        RemoteEndpoint = remoteEndpoint;
        _token = token;
    }

    /// <summary>
    /// Returns the next 24-bit notification sequence number (RFC 7641, Section 4.4).
    /// </summary>
    /// <remarks>
    /// The counter starts at <c>1</c>, is monotonically increasing within the 24-bit window, and
    /// wraps around after 16 777 215 notifications. Clients use the value to detect and reorder
    /// out-of-date notifications.
    /// </remarks>
    /// <example>
    /// <para>Each <see cref="CoapServer"/> notification calls this; custom publishers should mirror the same monotonic sequence on the wire.</para>
    /// <code>
    /// int seq = entry.NextSequenceNumber();
    /// var bytes = CoapMessage.BuildResponse(
    ///     CoapMessageType.NonConfirmable, code, messageId, token, format, payload, observe: seq);
    /// </code>
    /// </example>
    public int NextSequenceNumber() =>
        Interlocked.Increment(ref _sequenceNumber) & CoapConstants.ObserveSequenceMask;
}

internal readonly struct ObserverKey : IEquatable<ObserverKey>
{
    public IPEndPoint RemoteEndpoint { get; }
    private readonly byte[] _token;

    public ObserverKey(IPEndPoint remoteEndpoint, byte[] token)
    {
        RemoteEndpoint = remoteEndpoint;
        _token = token;
    }

    public bool Equals(ObserverKey other) =>
        RemoteEndpoint.Equals(other.RemoteEndpoint) &&
        _token.AsSpan().SequenceEqual(other._token);

    public override bool Equals(object? obj) => obj is ObserverKey other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RemoteEndpoint);
        foreach (var b in _token)
            hash.Add(b);
        return hash.ToHashCode();
    }
}
