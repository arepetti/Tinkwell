using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Tinkwell.Bootstrapper;

/// <summary>
/// Loads strategy assemblies based on a specified root namespac and task name.
/// </summary>
public static class StrategyAssemblyLoader
{
    /// <summary>
    /// Finds and loads all assemblies in the specified directory that match the given
    /// root namespace and task name.
    /// <para>
    /// The method searches for assemblies whose filenames start with <paramref name="rootNamespace"/>,
    /// followed by a dot, then <paramref name="taskName"/>, and optionally followed by a dot and a
    /// subtask or platform suffix (e.g., <c>.Windows</c>, <c>.Linux</c>, <c>.OSX</c>).
    /// Only assemblies matching the current platform or platform-independent assemblies are loaded.
    /// </para>
    /// </summary>
    /// <param name="rootNamespace">
    /// The root namespace. All assemblies must start with this value in their filename.
    /// For example, <c>Tinkwell.Strategy</c>.
    /// </param>
    /// <param name="taskName">
    /// The task name. All assemblies must continue with this value after the namespace.
    /// For example, <c>Agent</c>. Can be an empty string to match all tasks.
    /// </param>
    /// <param name="path">
    /// The directory path to search for assemblies. For example, <c>bin/Debug/net9.0</c>.
    /// </param>
    /// <param name="options">
    /// The search option, such as <see cref="SearchOption.TopDirectoryOnly"/> or
    /// <see cref="SearchOption.AllDirectories"/>.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{Assembly}"/> containing all assemblies that match the specified
    /// root namespace and task name in the given directory.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The search is performed in the specified <paramref name="path"/> directory.
    /// The filename pattern is: <c>{rootNamespace}.{taskName}[.Subtask][.Platform].dll</c>
    /// </para>
    /// </remarks>
    /// <example>
    /// Suppose the current platform is Windows and the directory contains the following DLLs:
    /// <code>
    /// Tinkwell.Strategy.Agent.dll
    /// Tinkwell.Strategy.Agent.Windows.dll
    /// Tinkwell.Strategy.Agent.Linux.dll
    /// Tinkwell.Strategy.Agent.Subtask.Windows.dll
    /// Tinkwell.Strategy.Agent.Subtask.Linux.dll
    /// Tinkwell.Strategy.Other.dll
    /// </code>
    /// Calling <c>LoadAssemblies("Tinkwell.Strategy", "Agent", "./plugins", SearchOption.TopDirectoryOnly)</c> will load:
    /// <list type="bullet">
    ///   <item><description><c>Tinkwell.Strategy.Agent.dll</c> (platform-independent)</description></item>
    ///   <item><description><c>Tinkwell.Strategy.Agent.Windows.dll</c> (matches current platform)</description></item>
    ///   <item><description><c>Tinkwell.Strategy.Agent.Subtask.Windows.dll</c> (matches current platform and subtask)</description></item>
    /// </list>
    /// It will NOT load:
    /// <list type="bullet">
    ///   <item><description><c>Tinkwell.Strategy.Agent.Linux.dll</c> (platform mismatch)</description></item>
    ///   <item><description><c>Tinkwell.Strategy.Agent.Subtask.Linux.dll</c> (platform mismatch)</description></item>
    ///   <item><description><c>Tinkwell.Strategy.Other.dll</c> (task name mismatch)</description></item>
    /// </list>
    /// </example>
    public static IEnumerable<Assembly> LoadAssemblies(string rootNamespace, string taskName, string path, SearchOption options)
    {
        var files = Directory.GetFiles(path, "*.dll", options);
        if (files.Length == 0)
            yield break;

        // We assume that the first file is representative of the entire directory but
        // it's not necessarily true. To check for each one woud be too expensive so we leave this
        // minor edge case alone.
        bool isFsCaseSensitive = IsFileSystemCaseSensitive(files.First());
        var platform = GetPlatformSuffix();

        var regex = new Regex(
            @$"^{Regex.Escape(rootNamespace)}\.{Regex.Escape(taskName)}(?:\.(\w+))?(?:\.(Windows|Linux|OSX))?\.dll$",
            isFsCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase
        );

        foreach (var file in files)
        {
            var filename = Path.GetFileName(file);
            var match = regex.Match(filename);

            if (!match.Success)
                continue;

            var taskPart = match.Groups[1].Value;
            var platformPart = match.Groups[2].Value;

            if (!IsCorrectPlatform(platform, platformPart, isFsCaseSensitive))
                continue;

            yield return Assembly.LoadFrom(file);
        }
    }

    /// <summary>
    /// Finds and loads all assemblies in the current directory that match the specified root namespace and task name.
    /// <para>
    /// The method searches for assemblies whose filenames start with <paramref name="rootNamespace"/>, followed by a dot,
    /// then <paramref name="taskName"/>, and optionally followed by a dot and a subtask or platform suffix (e.g., <c>.Windows</c>, <c>.Linux</c>, <c>.OSX</c>).
    /// Only assemblies matching the current platform or platform-independent assemblies are loaded.
    /// </para>
    /// </summary>
    /// <param name="rootNamespace">
    /// The root namespace. All assemblies must start with this value in their filename.
    /// For example, <c>Tinkwell.Strategy</c>.
    /// </param>
    /// <param name="taskName">
    /// The task name. All assemblies must continue with this value after the namespace.
    /// For example, <c>Agent</c>. Can be an empty string to match all tasks.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{Assembly}"/> containing all assemblies that match the specified root namespace and task name.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The search is performed in the directory of the currently executing assembly.
    /// The filename pattern is: <c>{rootNamespace}.{taskName}[.Subtask][.Platform].dll</c>
    /// </para>
    /// </remarks>
    /// <example>
    /// Suppose the current platform is Windows and the directory contains the following DLLs:
    /// <code>
    /// Tinkwell.Strategy.Agent.dll
    /// Tinkwell.Strategy.Agent.Windows.dll
    /// Tinkwell.Strategy.Agent.Linux.dll
    /// Tinkwell.Strategy.Agent.Subtask.Windows.dll
    /// Tinkwell.Strategy.Agent.Subtask.Linux.dll
    /// Tinkwell.Strategy.Other.dll
    /// </code>
    /// Calling <c>LoadAssemblies("Tinkwell.Strategy", "Agent")</c> will load:
    /// <list type="bullet">
    ///   <item><description><c>Tinkwell.Strategy.Agent.dll</c> (platform-independent)</description></item>
    ///   <item><description><c>Tinkwell.Strategy.Agent.Windows.dll</c> (matches current platform)</description></item>
    ///   <item><description><c>Tinkwell.Strategy.Agent.Subtask.Windows.dll</c> (matches current platform and subtask)</description></item>
    /// </list>
    /// It will NOT load:
    /// <list type="bullet">
    ///   <item><description><c>Tinkwell.Strategy.Agent.Linux.dll</c> (platform mismatch)</description></item>
    ///   <item><description><c>Tinkwell.Strategy.Agent.Subtask.Linux.dll</c> (platform mismatch)</description></item>
    ///   <item><description><c>Tinkwell.Strategy.Other.dll</c> (task name mismatch)</description></item>
    /// </list>
    /// </example>
    public static IEnumerable<Assembly> LoadAssemblies(string rootNamespace, string taskName)
        => LoadAssemblies(rootNamespace, taskName, GetExecutingAssemblyDirectoryName(), SearchOption.TopDirectoryOnly);

    /// <summary>
    /// Obtains all types in the specified assembly that implement the specified interface or class type.
    /// </summary>
    /// <typeparam name="T">The interface implemented by the types or a base class.</typeparam>
    /// <param name="assembly">The assembly where types should be searched.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">If <paramref name="assembly"/> is <c>null</c>.</exception>
    public static Type[] FindTypesImplementing<T>(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly.GetTypes()
            .Where(type => typeof(T).IsAssignableFrom(type) && !type.IsAbstract && type.IsClass)
            .ToArray();
    }

    /// <summary>
    /// Gets the directory path of the application entry point assembly.
    /// </summary>
    /// <returns>
    /// The directory where the entry assembly is or the current directory if it cannot be determined.
    /// </returns>
    public static string GetEntryAssemblyDirectoryName()
    {
        try
        {
            // If running in a context without an entry assembly, like some tests
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly is null)
                return Environment.CurrentDirectory;

           return Path.GetDirectoryName(entryAssembly.Location) ?? Environment.CurrentDirectory;
        }
        catch (NotSupportedException)
        {
            // In some environments (like Blazor WebAssembly), the entry assembly
            // location may not be available. We do not really expect this code to be called
            // in those environments, but if it does, we return the current directory.
            return Environment.CurrentDirectory;
        }
    }

    internal static string GetExecutingAssemblyDirectoryName()
        => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;

    private static bool IsCorrectPlatform(string systemPlatform, string filePlatform, bool isFsCaseSensitive)
    {
        if (string.IsNullOrEmpty(filePlatform))
            return true; // No filePlatform specified, so it's platform-indipendent.

        return filePlatform.Equals(systemPlatform,
            isFsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPlatformSuffix()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "OSX";

        return "Unknown";
    }

    private static bool IsFileSystemCaseSensitive(string path)
    {
        if (!File.Exists(path))
            return false;

        // To see if it's case-sensitive we try to read the given file
        // using a different case for the file name.
        var directoryPath = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);

        var altCaseFileName = fileName!.ToUpperInvariant() == fileName
            ? fileName.ToLowerInvariant()
            : fileName.ToUpperInvariant();

        var variant = Path.Combine(directoryPath!, altCaseFileName);

        // If I can't find the "variant" then the file system is case-sensitive.
        if (!File.Exists(variant))
            return true;

        // Both exist, let's check if they point to the same file
        return new FileInfo(path).FullName != new FileInfo(variant).FullName;
    }
}
