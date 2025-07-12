using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Tinkwell.Bootstrapper;

namespace Tinkwell.Actions.Executor;

public static class AgentsRecruiter
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    internal static Type? FindAgent(string name)
    {
        _types ??= StrategyAssemblyLoader.FindTypesImplementing<IAgent>(typeof(AgentsRecruiter).Assembly);

        return _types.FirstOrDefault(type =>
            string.Equals(name, type.GetCustomAttribute<AgentAttribute>()?.Name, StringComparison.Ordinal));
    }

    public static bool Exists(string name)
        => FindAgent(name) is not null;

    private static Type[]? _types;
}