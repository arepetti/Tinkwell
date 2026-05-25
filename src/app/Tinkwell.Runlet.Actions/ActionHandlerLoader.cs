using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tinkwell;
using Tinkwell.Actions.Abstractions;
using Tinkwell.Configuration.Actions;

namespace Tinkwell.Runlet.Actions;

/// <summary>
/// Loads external action handler assemblies and discovers <see cref="IActionHandler"/>
/// implementations. Unlike <see cref="Runner.Hosting.RunletLoader"/>, a single assembly
/// can export multiple handlers.
/// </summary>
public static class ActionHandlerLoader
{
    /// <summary>
    /// Scans the action definitions for unique <c>from</c> assembly references,
    /// loads each assembly, instantiates all discovered <see cref="IActionHandler"/>
    /// implementations via <see cref="ActivatorUtilities"/>, and returns them.
    /// </summary>
    [RequiresUnreferencedCode("Action handler discovery uses reflection.")]
    public static List<IActionHandler> LoadExternalHandlers(
        ActionsConfig config,
        IServiceProvider services,
        ILogger logger,
        PluginResolver? pluginResolver = null)
    {
        var assemblyNames = config.Actions
            .SelectMany(a => a.Handlers)
            .Select(h => h.AssemblyPath ?? ActionHandlerDefaults.DefaultAssembly)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return ActionHandlerDiscovery.LoadHandlers(assemblyNames, services, logger, pluginResolver);
    }
}
