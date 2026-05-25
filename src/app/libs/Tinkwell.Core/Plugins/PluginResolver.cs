using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Tinkwell;

/// <summary>
/// Resolves and loads plugin assemblies from the <see cref="PluginCatalog"/>.
/// Caches <see cref="PluginLoadContext"/> instances so the same plugin
/// directory always uses the same <see cref="System.Runtime.Loader.AssemblyLoadContext"/>.
/// Thread-safe.
/// </summary>
public sealed class PluginResolver
{
    private readonly PluginCatalog _catalog;
    private readonly string _hostBaseDir;
    private readonly ILogger _logger;
    private readonly Dictionary<string, PluginLoadContext> _contexts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>Creates a resolver that loads plugins from the given <paramref name="catalog"/>.</summary>
    public PluginResolver(PluginCatalog catalog, ILogger logger)
        : this(catalog, AppContext.BaseDirectory, logger) { }

    internal PluginResolver(PluginCatalog catalog, string hostBaseDir, ILogger logger)
    {
        _catalog = catalog;
        _hostBaseDir = hostBaseDir;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to load an assembly from a plugin. Returns <c>null</c> if
    /// no plugin provides the given assembly filename.
    /// </summary>
    public Assembly? TryLoadAssembly(string assemblyFileName)
    {
        var entry = _catalog.Resolve(assemblyFileName);
        if (entry is null)
            return null;

        var context = GetOrCreateContext(entry);
        var assemblyPath = Path.Combine(entry.Directory, assemblyFileName);

        if (!File.Exists(assemblyPath))
        {
            _logger.LogWarning(
                "Plugin '{Name}@{Version}' declares '{Assembly}' but file not found at '{Path}'",
                entry.Name, entry.Version, assemblyFileName, assemblyPath);
            return null;
        }

        _logger.LogDebug(
            "Loading '{Assembly}' from plugin '{Name}@{Version}' ({Dir})",
            assemblyFileName, entry.Name, entry.Version, entry.Directory);

        var assembly = context.LoadFromAssemblyPath(assemblyPath);
        ValidateCompatibility(assembly);
        return assembly;
    }

    /// <summary>
    /// Checks whether a plugin assembly's Tinkwell references are compatible
    /// with the host's loaded versions. Logs warnings on mismatch.
    /// </summary>
    public void ValidateCompatibility(Assembly pluginAssembly)
    {
        foreach (var refName in pluginAssembly.GetReferencedAssemblies())
        {
            if (refName.Name is null || !refName.Name.StartsWith("Tinkwell.", StringComparison.Ordinal))
                continue;

            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == refName.Name);

            if (loaded is null)
                continue;

            var hostVersion = loaded.GetName().Version;
            var pluginVersion = refName.Version;

            if (hostVersion is null || pluginVersion is null)
                continue;

            if (hostVersion.Major != pluginVersion.Major)
            {
                _logger.LogWarning(
                    "Plugin '{Plugin}' references {Ref} v{PluginVer} but host has v{HostVer} (major version mismatch)",
                    pluginAssembly.GetName().Name, refName.Name, pluginVersion, hostVersion);
            }
            else if (hostVersion < pluginVersion)
            {
                _logger.LogWarning(
                    "Plugin '{Plugin}' references {Ref} v{PluginVer} but host has older v{HostVer}",
                    pluginAssembly.GetName().Name, refName.Name, pluginVersion, hostVersion);
            }
        }
    }

    private PluginLoadContext GetOrCreateContext(PluginEntry entry)
    {
        lock (_lock)
        {
            if (_contexts.TryGetValue(entry.Directory, out var existing))
                return existing;

            var context = new PluginLoadContext(entry.Directory, _hostBaseDir);
            _contexts[entry.Directory] = context;
            return context;
        }
    }
}
