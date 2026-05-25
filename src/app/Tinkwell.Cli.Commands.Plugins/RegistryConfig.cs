namespace Tinkwell.Cli.Commands.Plugins;

/// <summary>
/// Resolves registry configuration from (highest priority first):
/// command-line argument, environment variable, configuration file.
/// </summary>
internal static class RegistryConfig
{
    private const string EnvUrl = "TW_REGISTRY_URL";
    private const string EnvPublicKey = "TW_REGISTRY_PUBLIC_KEY_FILE";
    private const string EnvGitHubRepo = "TW_GITHUB_REPO";
    private const string DefaultGitHubRepo = "arepetti/tinkwell-static-plugins-registry";

    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tinkwell");

    private static readonly string ConfigFile = Path.Combine(ConfigDir, "registry.json");

    public static string ResolveUrl(string? cliOverride)
    {
        if (!string.IsNullOrWhiteSpace(cliOverride))
            return cliOverride.TrimEnd('/');

        var envVal = Environment.GetEnvironmentVariable(EnvUrl);
        if (!string.IsNullOrWhiteSpace(envVal))
            return envVal.TrimEnd('/');

        var cfg = ReadConfig();
        if (!string.IsNullOrWhiteSpace(cfg.Url))
            return cfg.Url.TrimEnd('/');

        return string.Empty;
    }

    public static string? ResolvePublicKeyFile(string? cliOverride)
    {
        if (!string.IsNullOrWhiteSpace(cliOverride))
            return cliOverride;

        var envVal = Environment.GetEnvironmentVariable(EnvPublicKey);
        if (!string.IsNullOrWhiteSpace(envVal))
            return envVal;

        var cfg = ReadConfig();
        return string.IsNullOrWhiteSpace(cfg.PublicKeyFile) ? null : cfg.PublicKeyFile;
    }

    /// <summary>
    /// Resolves the GitHub repository used as a fallback plugin source.
    /// Always returns a value (falls back to <see cref="DefaultGitHubRepo"/>).
    /// </summary>
    public static string ResolveGitHubRepo(string? cliOverride)
    {
        if (!string.IsNullOrWhiteSpace(cliOverride))
            return cliOverride;

        var envVal = Environment.GetEnvironmentVariable(EnvGitHubRepo);
        if (!string.IsNullOrWhiteSpace(envVal))
            return envVal;

        var cfg = ReadConfig();
        if (!string.IsNullOrWhiteSpace(cfg.GitHubRepo))
            return cfg.GitHubRepo;

        return DefaultGitHubRepo;
    }

    private static RegistryConfigData ReadConfig()
    {
        if (!File.Exists(ConfigFile))
            return new();

        try
        {
            var json = File.ReadAllText(ConfigFile);
            return System.Text.Json.JsonSerializer.Deserialize<RegistryConfigData>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new();
        }
        catch
        {
            return new();
        }
    }

    private sealed class RegistryConfigData
    {
        public string? Url { get; set; }
        public string? PublicKeyFile { get; set; }
        public string? GitHubRepo { get; set; }
    }
}
