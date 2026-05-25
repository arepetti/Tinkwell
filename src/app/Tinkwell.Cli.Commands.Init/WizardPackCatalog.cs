namespace Tinkwell.Cli.Commands.Init;

/// <summary>
/// Discovers wizard packs from app-local directories and optional
/// additional roots (explicit path, environment variable).
/// </summary>
internal sealed class WizardPackCatalog
{
    private const string PacksSubdirectory = "packs";
    private const string InitSubdirectory = "init";
    private const string PackManifestFileName = "package.tw";
    private const string EnvironmentVariable = "TINKWELL_INIT_PACK_PATH";

    private readonly List<string> _roots = [];

    public WizardPackCatalog(string? explicitPackPath = null)
    {
        var appLocal = Path.Combine(AppContext.BaseDirectory, PacksSubdirectory, InitSubdirectory);
        if (Directory.Exists(appLocal))
            _roots.Add(appLocal);

        var envPath = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envPath) && Directory.Exists(envPath))
            _roots.Add(envPath);

        if (!string.IsNullOrWhiteSpace(explicitPackPath) && Directory.Exists(explicitPackPath))
            _roots.Add(explicitPackPath);
    }

    /// <summary>
    /// Returns the directories of all discovered packs. Each directory
    /// contains a <c>package.tw</c> manifest.
    /// </summary>
    public IReadOnlyList<string> DiscoverPackDirectories()
    {
        var result = new List<string>();

        foreach (var root in _roots)
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                if (File.Exists(Path.Combine(dir, PackManifestFileName)))
                    result.Add(dir);
            }
        }

        return result;
    }

    /// <summary>
    /// Finds a pack directory by name. Returns <see langword="null"/> if
    /// no matching pack is found.
    /// </summary>
    public string? FindPackDirectory(string packName)
    {
        foreach (var root in _roots)
        {
            var candidate = Path.Combine(root, packName);
            if (Directory.Exists(candidate)
                && File.Exists(Path.Combine(candidate, PackManifestFileName)))
            {
                return candidate;
            }
        }

        return null;
    }
}
