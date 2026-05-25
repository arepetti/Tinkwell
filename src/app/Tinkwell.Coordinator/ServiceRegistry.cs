using Tinkwell.Runner;

namespace Tinkwell.Coordinator;

/// <summary>
/// Thread-safe, coordinator-wide registry of services reported by runners.
/// Provides lookup by name, alias, and family across all runners.
/// </summary>
internal sealed class ServiceRegistry
{
    private readonly RunnerRegistry _runners;

    public ServiceRegistry(RunnerRegistry runners)
    {
        _runners = runners;
    }

    /// <summary>
    /// Finds the first service matching <paramref name="name"/> by exact
    /// match on service name, then aliases, then family name.
    /// </summary>
    public ServiceDefinition? Find(string name)
    {
        // Single pass over all runners, preserving name → alias → family precedence
        // (first match in iteration order for each category).
        ServiceDefinition? byName = null;
        ServiceDefinition? byAlias = null;
        ServiceDefinition? byFamily = null;

        foreach (var runner in _runners.All)
        {
            foreach (var service in runner.Services)
            {
                if (byName is null && string.Equals(service.Name, name, StringComparison.Ordinal))
                    byName = service;
                if (byAlias is null && service.Aliases.Any(a => string.Equals(a, name, StringComparison.Ordinal)))
                    byAlias = service;
                if (byFamily is null && string.Equals(service.FamilyName, name, StringComparison.Ordinal))
                    byFamily = service;
            }
        }

        return byName ?? byAlias ?? byFamily;
    }

    /// <summary>
    /// Returns all registered services, optionally filtered by a query string
    /// that matches against name, aliases, or family name.
    /// </summary>
    public IReadOnlyList<ServiceDefinition> List(string? query = null)
    {
        var results = new List<ServiceDefinition>();

        foreach (var runner in _runners.All)
        {
            foreach (var service in runner.Services)
            {
                if (string.IsNullOrWhiteSpace(query) || Matches(service, query))
                    results.Add(service);
            }
        }

        return results;
    }

    private static bool Matches(ServiceDefinition service, string query)
    {
        if (service.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (service.FamilyName is not null &&
            service.FamilyName.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (service.FriendlyName is not null &&
            service.FriendlyName.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        return service.Aliases.Any(a =>
            a.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
