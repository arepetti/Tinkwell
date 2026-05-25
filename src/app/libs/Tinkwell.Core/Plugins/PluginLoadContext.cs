using System.Reflection;
using System.Runtime.Loader;

namespace Tinkwell;

/// <summary>
/// Custom <see cref="AssemblyLoadContext"/> for a single plugin directory.
/// Shared assemblies (those present in the host directory) are always
/// resolved from the Default ALC; plugin-private assemblies are resolved
/// from the plugin directory.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly string[] RuntimePrefixes = ["System.", "Microsoft.", "netstandard"];

    private readonly string _pluginDir;
    private readonly string _hostBaseDir;
    private readonly AssemblyDependencyResolver? _depsResolver;

    public PluginLoadContext(string pluginDir, string hostBaseDir)
        : base($"plugin:{Path.GetFileName(pluginDir)}", isCollectible: false)
    {
        _pluginDir = pluginDir;
        _hostBaseDir = hostBaseDir;

        var mainDll = Directory.EnumerateFiles(pluginDir, "*.dll").FirstOrDefault();
        if (mainDll is not null)
        {
            var depsJson = Path.ChangeExtension(mainDll, ".deps.json");
            if (File.Exists(depsJson))
                _depsResolver = new AssemblyDependencyResolver(mainDll);
        }
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (name is null)
            return null;

        // Tier 1: runtime prefixes -- always from host, no disk check needed
        foreach (var prefix in RuntimePrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return null;
        }

        // Tier 2: host directory probe -- if the DLL exists alongside the host,
        // it's shared (Tinkwell.*, bundled third-party libs, etc.)
        var hostCandidate = Path.Combine(_hostBaseDir, name + ".dll");
        if (File.Exists(hostCandidate))
            return null;

        // Tier 3: plugin resolution via .deps.json
        if (_depsResolver is not null)
        {
            var resolved = _depsResolver.ResolveAssemblyToPath(assemblyName);
            if (resolved is not null)
                return LoadFromAssemblyPath(resolved);
        }

        // Tier 4: direct probe in plugin directory
        var pluginCandidate = Path.Combine(_pluginDir, name + ".dll");
        if (File.Exists(pluginCandidate))
            return LoadFromAssemblyPath(pluginCandidate);

        return null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        if (_depsResolver is not null)
        {
            var path = _depsResolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (path is not null)
                return LoadUnmanagedDllFromPath(path);
        }

        return nint.Zero;
    }
}
