using Microsoft.Extensions.Logging;

namespace Tinkwell;

/// <summary>
/// Scans plugin source directories and indexes discovered plugins.
/// Directories are named <c>{name}@{major}.{minor}.{patch}</c>.
/// </summary>
public sealed class PluginCatalog
{
    /// <summary>Environment variable that supplies additional plugin search directories.</summary>
    public const string EnvironmentVariable = "TINKWELL_PLUGIN_PATH";
    private const string TinkwellSubDir = "Tinkwell";
    private const string PluginsSubDir = "plugins";

    private readonly IReadOnlyList<string> _roots;
    private readonly ILogger? _logger;
    private List<PluginEntry>? _plugins;
    private readonly object _lock = new();

    /// <summary>Creates a catalog that scans the default plugin directories.</summary>
    public PluginCatalog(ILogger? logger = null)
        : this(GetDefaultPluginRoots(), logger) { }

    /// <summary>Creates a catalog that scans <paramref name="pluginRoots"/>.</summary>
    public PluginCatalog(IReadOnlyList<string> pluginRoots, ILogger? logger = null)
    {
        _roots = pluginRoots;
        _logger = logger;
    }

    /// <summary>
    /// All discovered plugins, deduplicated by (name, version) with
    /// higher-priority source winning ties. Sorted by name ascending,
    /// then version descending.
    /// </summary>
    public IReadOnlyList<PluginEntry> Plugins
    {
        get
        {
            EnsureScanned();
            return _plugins!;
        }
    }

    /// <summary>
    /// Forces a rescan of all plugin directories.
    /// </summary>
    public void Scan()
    {
        lock (_lock)
        {
            _plugins = ScanAll();
        }
    }

    /// <summary>
    /// Resolves a plugin by assembly filename (e.g., <c>"My.Runlet.Json.dll"</c>).
    /// Returns the plugin with the highest version containing that file.
    /// On version tie, the higher-priority source wins.
    /// Plugins with a non-null <see cref="PluginEntry.Subtype"/> are skipped.
    /// </summary>
    public PluginEntry? Resolve(string assemblyFileName)
    {
        EnsureScanned();

        PluginEntry? best = null;
        foreach (var plugin in _plugins!)
        {
            if (plugin.Subtype is not null)
                continue;

            if (!plugin.Assemblies.Contains(assemblyFileName, StringComparer.OrdinalIgnoreCase))
                continue;

            if (best is null || IsBetterMatch(plugin, best))
                best = plugin;
        }

        return best;
    }

    /// <summary>
    /// Resolves a plugin by plugin name with an optional minimum version.
    /// Returns the plugin with the highest version meeting the constraint.
    /// Plugins with a non-null <see cref="PluginEntry.Subtype"/> are skipped.
    /// </summary>
    public PluginEntry? Resolve(string pluginName, Version? minVersion = null)
    {
        EnsureScanned();

        PluginEntry? best = null;
        foreach (var plugin in _plugins!)
        {
            if (plugin.Subtype is not null)
                continue;

            if (!plugin.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (minVersion is not null && plugin.Version < minVersion)
                continue;

            if (best is null || IsBetterMatch(plugin, best))
                best = plugin;
        }

        return best;
    }

    /// <summary>
    /// Returns all plugins matching the given <paramref name="subtype"/>,
    /// sorted by name ascending then version descending.
    /// </summary>
    public IReadOnlyList<PluginEntry> FindBySubtype(string subtype)
    {
        EnsureScanned();

        return _plugins!
            .Where(p => string.Equals(p.Subtype, subtype, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Returns the default plugin source directories in priority order.
    /// Non-existent directories are included (they are skipped at scan time).
    /// </summary>
    public static IReadOnlyList<string> GetDefaultPluginRoots()
    {
        var roots = new List<string>();

        var envPath = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            // PATH-style: ';' (Windows) and ':' (Unix); accept both for mixed/copied env values.
            var pathSeparators = new[] { ';', Path.PathSeparator };
            foreach (var segment in envPath.Split(pathSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = segment.Trim();
                if (trimmed.Length > 0)
                    roots.Add(trimmed);
            }
        }

        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userHome))
            roots.Add(Path.Combine(userHome, TinkwellSubDir, PluginsSubDir));

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
            roots.Add(Path.Combine(localAppData, TinkwellSubDir, PluginsSubDir));

        roots.Add(Path.Combine(AppContext.BaseDirectory, PluginsSubDir));

        return roots;
    }

    private void EnsureScanned()
    {
        if (_plugins is not null)
            return;
        lock (_lock)
        {
            _plugins ??= ScanAll();
        }
    }

    private List<PluginEntry> ScanAll()
    {
        var seen = new HashSet<(string Name, Version Version)>();
        var result = new List<PluginEntry>();

        for (int priority=0; priority < _roots.Count; ++priority)
        {
            var root = _roots[priority];
            if (!Directory.Exists(root))
                continue;

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var dirName = Path.GetFileName(dir);
                if (!TryParseDirectoryName(dirName, out var name, out var version))
                {
                    _logger?.LogWarning(
                        "Skipping plugin directory '{Dir}': expected format {{name}}@{{major}}.{{minor}}.{{patch}}",
                        dirName);
                    continue;
                }

                var key = (name, version);
                if (!seen.Add(key))
                    continue; // higher-priority source already has this name@version

                var assemblies = Directory.EnumerateFiles(dir, "*.dll")
                    .Select(Path.GetFileName)
                    .Where(f => f is not null)
                    .Cast<string>()
                    .ToList();

                if (assemblies.Count == 0)
                {
                    _logger?.LogWarning("Plugin directory '{Dir}' contains no DLLs", dirName);
                    continue;
                }

                var subtype = ReadSubtypeFromManifest(dir);
                result.Add(new PluginEntry(name, version, dir, assemblies, priority, subtype));
            }
        }

        result.Sort((a, b) =>
        {
            var cmp = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
                return cmp;
            return b.Version.CompareTo(a.Version); // descending
        });

        return result;
    }

    /// <summary>Parses a <c>name@version</c> directory name into its components.</summary>
    public static bool TryParseDirectoryName(
        string dirName, out string name, out Version version)
    {
        name = "";
        version = new Version();

        var atIndex = dirName.LastIndexOf('@');
        if (atIndex <= 0 || atIndex >= dirName.Length - 1)
            return false;

        name = dirName[..atIndex];
        return Version.TryParse(dirName[(atIndex + 1)..], out version!);
    }

    private static bool IsBetterMatch(PluginEntry candidate, PluginEntry current)
    {
        var versionCmp = candidate.Version.CompareTo(current.Version);
        if (versionCmp > 0)
            return true;
        if (versionCmp < 0)
            return false;
        return candidate.SourcePriority < current.SourcePriority; // lower = higher priority
    }

    /// <summary>
    /// Lightweight reader: scans <c>package.tw</c> for a <c>subtype</c>
    /// property without pulling in the full manifest parser from
    /// <c>Tinkwell.Package</c>.
    /// </summary>
    private static string? ReadSubtypeFromManifest(string pluginDir)
    {
        var manifest = Path.Combine(pluginDir, "package.tw");
        if (!File.Exists(manifest))
            return null;

        foreach (var line in File.ReadLines(manifest))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("subtype", StringComparison.OrdinalIgnoreCase))
                continue;

            var rest = trimmed.AsSpan(7).TrimStart();
            if (rest.Length == 0 || rest[0] != '=')
                continue;

            rest = rest[1..].Trim();
            if (rest.Length >= 2 && rest[0] == '"' && rest[^1] == '"')
                rest = rest[1..^1];

            var value = rest.Trim().ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }
}
