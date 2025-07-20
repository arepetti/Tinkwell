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
    /// Finds all assemblies in the specified directory that match the given
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
    /// An <see cref="IEnumerable{string}"/> containing all the path of the assemblies that match the specified
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
    /// Calling <c>FindAssemblies("Tinkwell.Strategy", "Agent", "./plugins", SearchOption.TopDirectoryOnly)</c> will return:
    /// <list type="bullet">
    ///   <item><description><c>Tinkwell.Strategy.Agent.dll</c> (platform-independent)</description></item>
    ///   <item><description><c>Tinkwell.Strategy.Agent.Windows.dll</c> (matches current platform)</description></item>
    ///   <item><description><c>Tinkwell.Strategy.Agent.Subtask.Windows.dll</c> (matches current platform and subtask)</description></item>
    /// </list>
    /// It will NOT return:
    /// <list type="bullet">
    ///   <item><description><c>Tinkwell.Strategy.Agent.Linux.dll</c> (platform mismatch)</description></item>
    ///   <item><description><c>Tinkwell.Strategy.Agent.Subtask.Linux.dll</c> (platform mismatch)</description></item>
    ///   <item><description><c>Tinkwell.Strategy.Other.dll</c> (task name mismatch)</description></item>
    /// </list>
    /// </example>
    public static IEnumerable<string> FindAssemblies(string rootNamespace, string taskName, string path, SearchOption options)
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

            yield return file;
        }
    }

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
    /// For a full example <see cref="FindAssemblies(string, string, string, SearchOption)"/>.
    /// </remarks>
    public static IEnumerable<Assembly> LoadAssemblies(string rootNamespace, string taskName, string path, SearchOption options)
        => FindAssemblies(rootNamespace, taskName, path, options).Select(Assembly.LoadFrom);

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
    /// For a full example <see cref="FindAssemblies(string, string, string, SearchOption)"/>.
    /// </remarks>
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

    /// <summary>
    /// Gets the directory path of the application.
    /// </summary>
    /// <returns>
    /// The directory where the application is installed.
    /// </returns>
    public static string GetAppPath()
        => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;

    /// <summary>
    /// Determines the string comparer to use when working with file system entries
    /// in the specified path.
    /// </summary>
    /// <param name="path"></param>
    /// <returns>A <c>StringComparison</c>.</returns>
    /// <remarks>
    /// This function always uses an <strong>ordinal</strong> string comparer, case-sensitive or not
    /// depending on the file system itself.
    /// Note that, in the case of directories, the directory itself could be
    /// in a case-sensitive file system but it could be the mounting point of
    /// a case-insensitive one (or vice-versa!). Also for files, network shares
    /// could make things even more complicate so use this with caution.
    /// </remarks>
    public static StringComparison ResolveStringComparisonForPath(string path)
    {
        return IsFileSystemCaseSensitive(path)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
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
        bool isFile = File.Exists(path);
        bool isDirectory = Directory.Exists(path);

        if (!isFile && !isDirectory)
            return false;

        // To see if it's case-sensitive we try to read the given file
        // using a different case for the file name.
        var directoryPath = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);

        var altCaseFileName = fileName!.ToUpperInvariant() == fileName
            ? fileName.ToLowerInvariant()
            : fileName.ToUpperInvariant();

        var variant = Path.Combine(directoryPath!, altCaseFileName);

        if (isFile)
        {
            // If I can't find the "variant" then the file system is case-sensitive.
            if (!File.Exists(variant))
                return true;

            // Both exist, let's check if they point to the same file/file
            return new FileInfo(path).FullName != new FileInfo(variant).FullName;
        }

        // It's a directory, similar checks...
        if (!Directory.Exists(variant))
            return true;

        return new DirectoryInfo(path).FullName != new DirectoryInfo(variant).FullName;
    }

}
