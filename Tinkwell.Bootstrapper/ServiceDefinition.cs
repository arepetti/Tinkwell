namespace Tinkwell.Bootstrapper;

public sealed class ServiceDefinition
{
    public string? FriendlyName { get; init; }

    public string? FamilyName { get; init; }

    public string? Name { get; init; }

    public string[] Aliases { get; init; } = [];

    public string? Host { get; init; }

    public string? Url { get; init; }
}
