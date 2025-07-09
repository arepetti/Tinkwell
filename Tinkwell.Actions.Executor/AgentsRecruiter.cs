using System.Reflection;

namespace Tinkwell.Actions.Executor;

public static class AgentsRecruiter
{
    internal static Type? FindAgent(string name)
    {
        _types ??= Assembly
            .GetExecutingAssembly()
            .GetExportedTypes() // Public types to avoid problems when trimming for native AOT
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IAgent).IsAssignableFrom(type))
            .ToArray();

        return _types.FirstOrDefault(type =>
            string.Equals(name, type.GetCustomAttribute<AgentAttribute>()?.Name, StringComparison.Ordinal));
    }

    public static bool Exists(string name)
        => FindAgent(name) is not null;

    private static Type[]? _types;
}