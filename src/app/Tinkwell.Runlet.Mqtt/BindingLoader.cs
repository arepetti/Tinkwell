using System.Reflection;
using Microsoft.Extensions.Logging;
using Tinkwell.Integration;

namespace Tinkwell.Runlet.Mqtt;

/// <summary>
/// Discovers <see cref="IIntegrationBinding"/> implementations from
/// referenced assemblies and registers them by name.
/// </summary>
internal static class BindingLoader
{
    /// <summary>
    /// Scans the set of assembly names referenced across all binding configs
    /// and loads <see cref="IIntegrationBinding"/> implementations from them.
    /// </summary>
    public static Dictionary<string, IIntegrationBinding> LoadBindings(
        IEnumerable<string> assemblyNames,
        IServiceProvider services,
        ILogger logger,
        PluginResolver? pluginResolver = null)
    {
        var bindings = new Dictionary<string, IIntegrationBinding>(StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyName in assemblyNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var dllName = assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    ? assemblyName : assemblyName + ".dll";

                var asm = pluginResolver?.TryLoadAssembly(dllName);
                if (asm is null)
                {
                    var asmName = Path.GetFileNameWithoutExtension(assemblyName);
                    asm = Assembly.Load(new AssemblyName(asmName));
                }

                var types = asm.GetExportedTypes()
                    .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && typeof(IIntegrationBinding).IsAssignableFrom(t));

                foreach (var type in types)
                {
                    IIntegrationBinding? instance = null;

                    foreach (var ctor in type.GetConstructors().OrderByDescending(c => c.GetParameters().Length))
                    {
                        var parameters = ctor.GetParameters();
                        var args = new object?[parameters.Length];
                        bool resolved = true;

                        for (int i=0; i < parameters.Length; ++i)
                        {
                            args[i] = services.GetService(parameters[i].ParameterType);
                            if (args[i] is null && !parameters[i].HasDefaultValue)
                            {
                                resolved = false;
                                break;
                            }
                        }

                        if (resolved)
                        {
                            instance = (IIntegrationBinding)ctor.Invoke(args);
                            break;
                        }
                    }

                    if (instance is null)
                    {
                        logger.LogWarning(
                            "Could not instantiate binding '{Type}' from '{Assembly}' — no suitable constructor",
                            type.FullName, assemblyName);
                        continue;
                    }

                    bindings[instance.Name] = instance;
                    logger.LogDebug("Loaded binding '{Name}' from '{Assembly}'",
                        instance.Name, assemblyName);
                }
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load bindings from '{Assembly}'", assemblyName);
            }
        }

        return bindings;
    }
}