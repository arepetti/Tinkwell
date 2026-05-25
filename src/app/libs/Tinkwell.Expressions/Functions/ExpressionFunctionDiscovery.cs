using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Tinkwell.Expressions.Functions;

/// <summary>
/// Discovers <see cref="IExpressionFunction"/> implementations in assemblies
/// using reflection. Results are cached in-process for the lifetime of the
/// <see cref="AppDomain"/>, keyed by the <see cref="Assembly"/> instance,
/// so repeated <see cref="FromAssembly"/> calls for the same assembly
/// return the same list without scanning again.
/// </summary>
/// <remarks>
/// This class is thread-safe. The static cache is shared across all
/// <see cref="ExpressionEvaluator"/> instances.
/// </remarks>
public static class ExpressionFunctionDiscovery
{
    private static readonly ConcurrentDictionary<Assembly, IReadOnlyList<IExpressionFunction>> Cache = new();

    /// <summary>
    /// Discovers all concrete <see cref="IExpressionFunction"/> implementations
    /// in the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>
    /// A cached, read-only list of function instances. Each concrete type is
    /// instantiated once via its parameterless constructor.
    /// </returns>
    [RequiresUnreferencedCode("Scans assembly types via reflection. Not compatible with trimming/AOT.")]
    public static IReadOnlyList<IExpressionFunction> FromAssembly(Assembly assembly)
    {
        return Cache.GetOrAdd(assembly, static asm =>
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var loader = string.Join(
                    "; ",
                    (ex.LoaderExceptions ?? Array.Empty<Exception>()).Select(e => e?.Message).Where(m => m is not null));
                throw new InvalidOperationException(
                    $"Failed to load one or more types from assembly '{asm.FullName}'." +
                    (string.IsNullOrEmpty(loader) ? "" : " Loader details: " + loader), ex);
            }

            return types
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                         && typeof(IExpressionFunction).IsAssignableFrom(t)
                         && t.GetConstructor(Type.EmptyTypes) is not null)
                .Select(t => (IExpressionFunction)Activator.CreateInstance(t)!)
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .ToList();
        });
    }

    /// <summary>
    /// Discovers all concrete <see cref="IExpressionFunction"/> implementations
    /// in the assembly containing <typeparamref name="TMarker"/>.
    /// </summary>
    /// <typeparam name="TMarker">
    /// Any type whose assembly should be scanned (e.g. a function class).
    /// </typeparam>
    [RequiresUnreferencedCode("Scans assembly types via reflection. Not compatible with trimming/AOT.")]
    public static IReadOnlyList<IExpressionFunction> FromAssemblyOf<TMarker>()
        => FromAssembly(typeof(TMarker).Assembly);

    /// <summary>
    /// Discovers all built-in functions shipped with this library.
    /// </summary>
    /// <remarks>
    /// Equivalent to <c>FromAssembly(assembly containing Tinkwell.Expressions)</c> —
    /// all concrete, parameterless <see cref="IExpressionFunction"/> types in
    /// this package's assembly. See the package README and the
    /// <c>Tinkwell.Expressions.Functions.Builtins</c> source folder for a
    /// human-readable function list.
    /// </remarks>
    [RequiresUnreferencedCode("Scans assembly types via reflection. Not compatible with trimming/AOT.")]
    public static IReadOnlyList<IExpressionFunction> BuiltIn()
        => FromAssembly(typeof(ExpressionFunctionDiscovery).Assembly);
}
