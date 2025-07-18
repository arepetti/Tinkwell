using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Tinkwell.Bootstrapper;

namespace Tinkwell.Actions.Executor;

static class AgentsRecruiter
{
    private static readonly List<Assembly> _additionalAssemblies = new();

    public static void RegisterAssembly(Assembly assembly)
    {
        _additionalAssemblies.Add(assembly);
        _types = null; // Invalidate the cache
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    internal static Type? FindAgent(string name)
    {
        if (_types is null)
        {
            var assemblies = new List<Assembly> { typeof(AgentsRecruiter).Assembly };
            assemblies.AddRange(_additionalAssemblies);
            _types = assemblies.SelectMany(StrategyAssemblyLoader.FindTypesImplementing<IAgent>).ToArray();
        }

        return _types.FirstOrDefault(type =>
            string.Equals(name, type.GetCustomAttribute<AgentAttribute>()?.Name, StringComparison.Ordinal));
    }

    public static bool Exists(string name)
        => FindAgent(name) is not null;

    private static Type[]? _types;
}
