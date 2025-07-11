namespace Tinkwell.Bootstrapper;

/// <summary>
/// Represents metadata for a service registered in Tinkwell.
/// </summary>
public sealed class ServiceDefinition
{
    /// <summary>
    /// Gets or sets the friendly name of the service.
    /// </summary>
    public string? FriendlyName { get; init; }

    /// <summary>
    /// Gets or sets the family name of the service.
    /// </summary>
    public string? FamilyName { get; init; }

    /// <summary>
    /// Gets or sets the name of the service.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets or sets the aliases for the service.
    /// </summary>
    public string[] Aliases { get; init; } = [];

    /// <summary>
    /// Gets or sets the host address of the service.
    /// </summary>
    public string? Host { get; init; }

    /// <summary>
    /// Gets or sets the URL of the service.
    /// </summary>
    public string? Url { get; init; }
}
