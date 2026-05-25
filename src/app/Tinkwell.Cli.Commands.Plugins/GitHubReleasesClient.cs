using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using NuGet.Versioning;

namespace Tinkwell.Cli.Commands.Plugins;

/// <summary>
/// Discovers and downloads plugin packages from GitHub Releases.
/// Unauthenticated (public repos only, 60 req/hour rate limit).
/// </summary>
internal sealed class GitHubReleasesClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _owner;
    private readonly string _repo;

    public GitHubReleasesClient(string ownerSlashRepo)
    {
        var parts = ownerSlashRepo.Split('/', 2);
        _owner = parts[0];
        _repo = parts.Length > 1 ? parts[1] : parts[0];

        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        _http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        _http.DefaultRequestHeaders.Add("User-Agent", BuildUserAgent());
    }

    /// <summary>
    /// Find the best matching release asset for a plugin.
    /// When <paramref name="version"/> is specified, fetches the exact release tag.
    /// Otherwise lists all releases and picks the highest semver for the plugin.
    /// </summary>
    public async Task<GitHubAssetMatch?> ResolveAsync(
        string pluginName, string? version, CancellationToken ct)
    {
        if (version is not null)
            return await ResolveExactVersionAsync(pluginName, version, ct);

        return await ResolveLatestVersionAsync(pluginName, ct);
    }

    /// <summary>
    /// Download an asset by its direct URL. Returns the response stream.
    /// </summary>
    public async Task<Stream> DownloadAssetAsync(string assetUrl, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, assetUrl);
        request.Headers.Add("Accept", "application/octet-stream");
        request.Headers.Add("User-Agent", BuildUserAgent());

        var client = new HttpClient();
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(ct);
    }

    private async Task<GitHubAssetMatch?> ResolveExactVersionAsync(
        string pluginName, string version, CancellationToken ct)
    {
        var tag = $"{pluginName}@{version}";
        var url = $"repos/{_owner}/{_repo}/releases/tags/{Uri.EscapeDataString(tag)}";

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url, ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
            return null;

        var text = await response.Content.ReadAsStringAsync(ct);
        var release = JsonDocument.Parse(text).RootElement;
        return MatchAsset(release, pluginName, version);
    }

    private async Task<GitHubAssetMatch?> ResolveLatestVersionAsync(
        string pluginName, CancellationToken ct)
    {
        var url = $"repos/{_owner}/{_repo}/releases?per_page=100";

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url, ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
            return null;

        var text = await response.Content.ReadAsStringAsync(ct);
        var releases = JsonDocument.Parse(text).RootElement;

        var tagPrefix = pluginName + "@";
        GitHubAssetMatch? best = null;

        foreach (var release in releases.EnumerateArray())
        {
            var tagName = release.GetProperty("tag_name").GetString();
            if (tagName is null || !tagName.StartsWith(tagPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var ver = tagName[tagPrefix.Length..];
            if (best is not null && CompareVersions(ver, best.Version) <= 0)
                continue;

            var match = MatchAsset(release, pluginName, ver);
            if (match is not null)
                best = match;
        }

        return best;
    }

    /// <summary>
    /// Finds the best asset in a release for the current platform.
    /// Resolution order: architecture-specific > platform-only > bare {name}-{version}.zip.
    /// </summary>
    private static GitHubAssetMatch? MatchAsset(
        JsonElement release, string pluginName, string version)
    {
        if (!release.TryGetProperty("assets", out var assets))
            return null;

        var arch = DetectArchitecture();
        var archFallback = DetectArchitectureFallback();

        var exactName = $"{pluginName}-{version}-{arch}.zip";
        var platformName = archFallback is not null
            ? $"{pluginName}-{version}-{archFallback}.zip"
            : null;
        var anyName = $"{pluginName}-{version}-Any.zip";
        var bareName = $"{pluginName}-{version}.zip";

        string? exactUrl = null;
        string? platformUrl = null;
        string? anyUrl = null;
        string? bareUrl = null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var downloadUrl = asset.GetProperty("browser_download_url").GetString();
            if (name is null || downloadUrl is null)
                continue;

            if (name.Equals(exactName, StringComparison.OrdinalIgnoreCase))
                exactUrl = downloadUrl;
            else if (platformName is not null && name.Equals(platformName, StringComparison.OrdinalIgnoreCase))
                platformUrl = downloadUrl;
            else if (name.Equals(anyName, StringComparison.OrdinalIgnoreCase))
                anyUrl = downloadUrl;
            else if (name.Equals(bareName, StringComparison.OrdinalIgnoreCase))
                bareUrl = downloadUrl;
        }

        var url = exactUrl ?? platformUrl ?? anyUrl ?? bareUrl;
        return url is not null ? new GitHubAssetMatch(pluginName, version, url) : null;
    }

    private static string DetectArchitecture()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "Windows_x64",
                Architecture.Arm64 => "Windows_arm64",
                _ => "Windows",
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X86 => "Linux_x86",
                Architecture.X64 => "Linux_x64",
                Architecture.Arm64 => "Linux_arm64",
                _ => "Linux",
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "MacOS_arm64",
                _ => "MacOS_arm64",
            };
        }

        return "Linux_x64";
    }

    private static string? DetectArchitectureFallback()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";
        return null;
    }

    private static int CompareVersions(string a, string b)
    {
        if (SemanticVersion.TryParse(a, out var sa) && SemanticVersion.TryParse(b, out var sb))
            return sa.CompareTo(sb);
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildUserAgent()
    {
        var version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version[..plusIndex];

        return $"Tinkwell-CLI/{version}";
    }

    public void Dispose() => _http.Dispose();
}

internal sealed record GitHubAssetMatch(string Name, string Version, string DownloadUrl);
