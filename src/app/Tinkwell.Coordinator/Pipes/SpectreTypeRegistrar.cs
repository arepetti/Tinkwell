using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Tinkwell.Coordinator.Pipes;

/// <summary>
/// Bridges the coordinator's <see cref="IServiceProvider"/> with
/// Spectre.Console.Cli's type registration system. Per-invocation
/// instances (like <see cref="PipeCommandContext"/>) are layered
/// on top of the host container.
/// </summary>
internal sealed class SpectreTypeRegistrar : ITypeRegistrar
{
    private readonly IServiceProvider _hostProvider;
    private readonly Dictionary<Type, object> _instances = [];

    public SpectreTypeRegistrar(IServiceProvider hostProvider)
    {
        _hostProvider = hostProvider;
    }

    public void RegisterInstance(Type type, object instance) =>
        _instances[type] = instance;

    public ITypeResolver Build() =>
        new SpectreTypeResolver(new LayeredServiceProvider(_hostProvider, _instances));

    public void Register(Type service, Type implementation) { }
    public void RegisterLazy(Type service, Func<object> factory) { }

    /// <summary>
    /// Composite provider that checks per-invocation instances first,
    /// then falls back to the host container.
    /// </summary>
    private sealed class LayeredServiceProvider(
        IServiceProvider host,
        IReadOnlyDictionary<Type, object> overrides) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            overrides.TryGetValue(serviceType, out var instance)
                ? instance
                : host.GetService(serviceType);
    }
}

/// <summary>
/// Resolves types for Spectre.Console.Cli commands using
/// <see cref="ActivatorUtilities"/> so that constructor injection works.
/// </summary>
internal sealed class SpectreTypeResolver(IServiceProvider services) : ITypeResolver
{
    public object? Resolve(Type? type)
    {
        if (type is null)
            return null;

        var registered = services.GetService(type);
        if (registered is not null)
            return registered;

        try
        {
            return ActivatorUtilities.CreateInstance(services, type);
        }
        catch
        {
            return Activator.CreateInstance(type);
        }
    }
}
