using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Actions.Abstractions;

/// <summary>
/// Loads action handler assemblies and discovers <see cref="IActionHandler"/>
/// implementations. Unlike the coordinator runlet loader, a single assembly
/// can export multiple handlers.
/// </summary>
public static class ActionHandlerDiscovery
{
    /// <summary>
    /// Loads each named assembly, instantiates all discovered <see cref="IActionHandler"/>
    /// implementations via <see cref="ActivatorUtilities"/>, and returns them.
    /// </summary>
    [RequiresUnreferencedCode("Action handler discovery uses reflection.")]
    public static List<IActionHandler> LoadHandlers(
        IEnumerable<string> assemblyNames,
        IServiceProvider services,
        ILogger logger,
        PluginResolver? pluginResolver = null)
    {
        var handlers = new List<IActionHandler>();

        var names = assemblyNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (names.Count == 0)
            return handlers;

        var probeDirectory = AppContext.BaseDirectory;

        foreach (var assemblyName in names)
        {
            var dllName = assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? assemblyName
                : assemblyName + ".dll";

            Assembly? assembly = pluginResolver?.TryLoadAssembly(dllName);
            if (assembly is not null)
                goto handlerDiscovery;

            var path = Path.Combine(probeDirectory, dllName);

            if (!File.Exists(path))
            {
                logger.LogError("Action handler assembly not found: '{Path}'", path);
                continue;
            }

            logger.LogDebug("Loading action handlers from: {Path}", path);

            try
            {
                assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load action handler assembly: {Path}", path);
                continue;
            }
            handlerDiscovery:

            var handlerTypes = assembly.GetExportedTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false }
                    && typeof(IActionHandler).IsAssignableFrom(t))
                .ToList();

            if (handlerTypes.Count == 0)
            {
                logger.LogWarning(
                    "Assembly '{Assembly}' contains no IActionHandler implementations",
                    assembly.GetName().Name);
                continue;
            }

            foreach (var handlerType in handlerTypes)
            {
                try
                {
                    var handler = (IActionHandler)ActivatorUtilities.CreateInstance(services, handlerType);
                    handlers.Add(handler);
                    logger.LogDebug("Loaded action handler: {Type} from {Assembly}",
                        handlerType.FullName, assembly.GetName().Name);
                }
                catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to instantiate handler {Type} from {Assembly}",
                        handlerType.FullName, assembly.GetName().Name);
                }
            }
        }

        return handlers;
    }
}