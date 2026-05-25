namespace Tinkwell.Runner;

/// <summary>
/// Mutable options passed to endpoint mapper registration methods so that
/// runlets can attach discovery metadata to a service. The mapper uses
/// these values (combined with the auto-resolved service name and host)
/// to produce a <see cref="ServiceDefinition"/>.
/// </summary>
public sealed class ServiceRegistrationOptions
{
    /// <summary>
    /// An optional human-readable display name for the service.
    /// </summary>
    public string? FriendlyName { get; set; }

    /// <summary>
    /// An optional group name for logically related services.
    /// </summary>
    public string? FamilyName { get; set; }

    /// <summary>
    /// Alternative names under which the service can be discovered.
    /// </summary>
    public IList<string> Aliases { get; } = [];
}
