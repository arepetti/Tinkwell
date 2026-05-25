using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Runner.Hosting;

/// <summary>
/// Loads runlet assemblies and discovers <see cref="IRunlet"/> implementations
/// via reflection. Each assembly must contain exactly one public class
/// implementing <see cref="IRunlet"/> (or a derived interface).
/// </summary>
public static class RunletLoader
{
    /// <summary>
    /// Loads all runlet assemblies described by the descriptors and instantiates
    /// their <see cref="IRunlet"/> implementations.
    /// </summary>
    /// <returns>
    /// A list of tuples pairing each descriptor with its instantiated runlet.
    /// </returns>
    [RequiresUnreferencedCode("Runlet discovery uses reflection to find IRunlet implementations.")]
    public static List<RunletState> LoadAll(
        IReadOnlyList<RunletDescriptor> descriptors,
        ILogger logger,
        PluginResolver? pluginResolver = null)
    {
        var probeDirectory = AppContext.BaseDirectory;
        InstallAssemblyResolver(probeDirectory, logger);

        var results = new List<RunletState>(descriptors.Count);

        foreach (var descriptor in descriptors)
        {
            Assembly assembly;

            // Try the plugin catalog first for bare filenames (no path separator)
            if (pluginResolver is not null &&
                !descriptor.AssemblyPath.Contains(Path.DirectorySeparatorChar) &&
                !descriptor.AssemblyPath.Contains(Path.AltDirectorySeparatorChar))
            {
                var dllName = descriptor.AssemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    ? descriptor.AssemblyPath
                    : descriptor.AssemblyPath + ".dll";

                var pluginAssembly = pluginResolver.TryLoadAssembly(dllName);
                if (pluginAssembly is not null)
                {
                    assembly = pluginAssembly;
                    goto discovered;
                }
            }

            var path = Path.GetFullPath(descriptor.AssemblyPath);
            logger.LogDebug("Loading runlet '{Name}' from '{Path}'", descriptor.Name, path);

            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Runlet assembly not found: '{path}' (runlet '{descriptor.Name}')");

            assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            discovered:
            var runletType = FindRunletType(assembly, descriptor.Name);
            var instance = (IRunlet)Activator.CreateInstance(runletType)!;

            logger.LogDebug(
                "Runlet '{Name}' loaded: {Type} (implements {Interfaces})",
                descriptor.Name, runletType.FullName,
                string.Join(", ", runletType.GetInterfaces()
                    .Where(i => typeof(IRunlet).IsAssignableFrom(i))
                    .Select(i => i.Name)));

            results.Add(new RunletState(descriptor, instance));
        }

        return results;
    }

    private static bool _resolverInstalled;

    private static void InstallAssemblyResolver(string probeDirectory, ILogger logger)
    {
        if (_resolverInstalled)
            return;
        _resolverInstalled = true;

        AssemblyLoadContext.Default.Resolving += (_, assemblyName) =>
        {
            var candidate = Path.Combine(probeDirectory, assemblyName.Name + ".dll");
            if (!File.Exists(candidate))
                return null;

            logger.LogDebug("Resolved runlet dependency '{Name}' from '{Path}'",
                assemblyName.Name, candidate);
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
        };
    }

    [RequiresUnreferencedCode("Runlet discovery uses reflection.")]
    private static Type FindRunletType(Assembly assembly, string runletName)
    {
        var candidates = assembly.GetExportedTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && typeof(IRunlet).IsAssignableFrom(t))
            .ToList();

        return candidates.Count switch
        {
            0 => throw new InvalidOperationException(
                $"Assembly '{assembly.GetName().Name}' does not contain a public class implementing IRunlet " +
                $"(runlet '{runletName}')"),
            1 => candidates[0],
            _ => throw new InvalidOperationException(
                $"Assembly '{assembly.GetName().Name}' contains {candidates.Count} IRunlet implementations " +
                $"({string.Join(", ", candidates.Select(t => t.Name))}); expected exactly one " +
                $"(runlet '{runletName}')")
        };
    }
}

/// <summary>
/// A loaded runlet paired with its descriptor.
/// </summary>
public sealed record RunletState(RunletDescriptor Descriptor, IRunlet Instance);
