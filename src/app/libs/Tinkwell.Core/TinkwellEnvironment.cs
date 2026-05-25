using System.Runtime.InteropServices;

namespace Tinkwell;

/// <summary>
/// Provides well-known paths and environment information for the Tinkwell
/// platform. All paths are lazily resolved on first access and cached for
/// the lifetime of the process.
/// </summary>
/// <remarks>
/// Each property checks for an environment variable override first,
/// falling back to a sensible platform default. Relative paths passed
/// to the <c>GetFull*Path</c> helpers are resolved against the
/// corresponding base directory. Path resolution is thread-safe: values
/// are initialized once on first access and cached.
/// </remarks>
public static class TinkwellEnvironment
{
    private const string DataEnvVar = "TINKWELL_DATA";
    private const string WorkDirEnvVar = "TINKWELL_WORKDIR";

    private const string NixSystemDataDir = "/var/lib/Tinkwell";
    private const string NixUserDataDir = ".local/share/Tinkwell";
    private const string AppName = "Tinkwell";

    private static readonly Lazy<string> _dataPath = new(ComputeDataPath, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<string> _nixDataPath = new(ComputeNixDataPath, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Directory where runlets and services store persistent data
    /// (databases, caches, state files).
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    ///   <item><c>TINKWELL_DATA</c> environment variable (if set)</item>
    ///   <item>Windows: <c>%APPDATA%\Tinkwell</c></item>
    ///   <item>Linux/macOS: <c>/var/lib/Tinkwell</c> if writable,
    ///         else <c>~/.local/share/Tinkwell</c></item>
    /// </list>
    /// </remarks>
    public static string DataPath => _dataPath.Value;

    /// <summary>
    /// The logical working directory for the current process. Relative
    /// configuration paths are resolved against this directory.
    /// </summary>
    /// <remarks>
    /// Returns the <c>TINKWELL_WORKDIR</c> environment variable if set,
    /// otherwise <see cref="Environment.CurrentDirectory"/>.
    /// </remarks>
    public static string WorkingDirectory =>
        Environment.GetEnvironmentVariable(WorkDirEnvVar)
        ?? Environment.CurrentDirectory;

    /// <summary>
    /// Resolves <paramref name="path"/> against <see cref="WorkingDirectory"/>
    /// if it is relative, or returns it unchanged if it is already fully
    /// qualified.
    /// </summary>
    public static string GetFullWorkingPath(string path) =>
        Path.IsPathFullyQualified(path) ? path : Path.Combine(WorkingDirectory, path);

    /// <summary>
    /// Resolves <paramref name="path"/> against <see cref="DataPath"/>
    /// if it is relative, or returns it unchanged if it is already fully
    /// qualified.
    /// </summary>
    public static string GetFullDataPath(string path) =>
        Path.IsPathFullyQualified(path) ? path : Path.Combine(DataPath, path);

    private static string ComputeDataPath()
    {
        var env = Environment.GetEnvironmentVariable(DataEnvVar);
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppName);
        }

        return _nixDataPath.Value;
    }

    private static string ComputeNixDataPath()
    {
        if (IsDirectoryWritable(NixSystemDataDir))
            return NixSystemDataDir;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            NixUserDataDir);
    }

    private static bool IsDirectoryWritable(string path)
    {
        var probe = Path.Combine(path, Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(path);
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
