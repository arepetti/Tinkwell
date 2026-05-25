using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using NuGet.Versioning;
using Tinkwell.Http;

namespace Tinkwell.Cli.Commands.Plugins;

/// <summary>
/// Lightweight HTTP client for the Tinkwell plugin registry API.
/// Used by the plugin install and search commands to resolve packages by name.
/// </summary>
internal sealed class PluginRegistryClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;

    public PluginRegistryClient(string baseUrl)
    {
        _http = new HttpClient(new RetryAfterHandler { InnerHandler = new HttpClientHandler() })
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
        };
    }

    public async Task<RegistryPackage?> ResolvePackageAsync(
        string handle, string name, string? version, CancellationToken ct)
    {
        var arch = DetectArchitecture();
        var archFallback = DetectArchitectureFallback();

        var filter = $"name=={name}";
        if (version is not null)
            filter += $",version=={version}";

        var url = $"packages?api-version=1.0&filter={Uri.EscapeDataString(filter)}&sort=-publishDate&pageSize=100";
        var response = await _http.GetAsync(url, ct);
        await EnsureSuccessAsync(response, ct);

        var text = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(text);

        if (!doc.RootElement.TryGetProperty("items", out var items))
            return null;

        RegistryPackage? bestExact = null;
        RegistryPackage? bestPlatform = null;
        RegistryPackage? bestAny = null;

        foreach (var item in items.EnumerateArray())
        {
            var pkg = ParsePackage(item);
            if (pkg is null)
                continue;

            if (!pkg.Author.Equals(handle, StringComparison.OrdinalIgnoreCase))
                continue;

            if (pkg.Architecture.Equals(arch, StringComparison.OrdinalIgnoreCase))
            {
                if (bestExact is null || CompareVersions(pkg.Version, bestExact.Version) > 0)
                    bestExact = pkg;
            }
            else if (archFallback is not null &&
                     pkg.Architecture.Equals(archFallback, StringComparison.OrdinalIgnoreCase))
            {
                if (bestPlatform is null || CompareVersions(pkg.Version, bestPlatform.Version) > 0)
                    bestPlatform = pkg;
            }
            else if (pkg.Architecture.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                if (bestAny is null || CompareVersions(pkg.Version, bestAny.Version) > 0)
                    bestAny = pkg;
            }
        }

        return bestExact ?? bestPlatform ?? bestAny;
    }

    public async Task<Stream> DownloadAsync(int packageId, CancellationToken ct)
    {
        var url = $"packages/{packageId}/download?api-version=1.0";
        var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadAsStreamAsync(ct);
    }

    /// <summary>
    /// Fetches the registry's public ECDSA key (SPKI, base64-encoded)
    /// from <c>/.well-known/registry-key</c>. Returns raw bytes or null on failure.
    /// </summary>
    public async Task<byte[]?> FetchRegistryPublicKeyAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync(".well-known/registry-key", ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var text = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("publicKey", out var keyEl) &&
                keyEl.ValueKind == JsonValueKind.String)
            {
                var base64 = keyEl.GetString();
                return base64 is not null ? Convert.FromBase64String(base64) : null;
            }
        }
        catch
        {
            // Best effort -- if the endpoint is not available, proceed without verification
        }

        return null;
    }

    public async Task<SearchResult> SearchAsync(
        string? filter, string? sort, int? pageSize, CancellationToken ct)
    {
        var queryParts = new List<string> { "api-version=1.0" };

        if (!string.IsNullOrWhiteSpace(filter))
            queryParts.Add($"filter={Uri.EscapeDataString(filter)}");
        if (!string.IsNullOrWhiteSpace(sort))
            queryParts.Add($"sort={Uri.EscapeDataString(sort)}");
        if (pageSize.HasValue)
            queryParts.Add($"pageSize={pageSize.Value}");

        var url = $"packages?{string.Join("&", queryParts)}";
        var response = await _http.GetAsync(url, ct);
        await EnsureSuccessAsync(response, ct);

        var text = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        var items = new List<JsonElement>();
        if (root.TryGetProperty("items", out var itemsEl))
        {
            foreach (var item in itemsEl.EnumerateArray())
                items.Add(item.Clone());
        }

        string? nextLink = null;
        if (root.TryGetProperty("nextLink", out var nlEl) && nlEl.ValueKind == JsonValueKind.String)
            nextLink = nlEl.GetString();

        return new SearchResult(items, nextLink);
    }

    public async Task<SearchResult> GetNextPageAsync(string nextLink, CancellationToken ct)
    {
        var response = await _http.GetAsync(nextLink, ct);
        await EnsureSuccessAsync(response, ct);

        var text = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        var items = new List<JsonElement>();
        if (root.TryGetProperty("items", out var itemsEl))
        {
            foreach (var item in itemsEl.EnumerateArray())
                items.Add(item.Clone());
        }

        string? nl = null;
        if (root.TryGetProperty("nextLink", out var nlEl) && nlEl.ValueKind == JsonValueKind.String)
            nl = nlEl.GetString();

        return new SearchResult(items, nl);
    }

    private static RegistryPackage? ParsePackage(JsonElement el)
    {
        if (!el.TryGetProperty("id", out var idEl))
            return null;
        if (!el.TryGetProperty("author", out var authorEl))
            return null;
        if (!el.TryGetProperty("name", out var nameEl))
            return null;
        if (!el.TryGetProperty("version", out var versionEl))
            return null;
        if (!el.TryGetProperty("architecture", out var archEl))
            return null;

        return new RegistryPackage(
            idEl.GetInt32(),
            authorEl.GetString() ?? "",
            nameEl.GetString() ?? "",
            versionEl.GetString() ?? "",
            archEl.GetString() ?? "");
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

    /// <summary>
    /// Returns the platform-agnostic fallback architecture (e.g. "Windows" for "Windows_x64").
    /// </summary>
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

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        string message;
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var msgEl))
                message = msgEl.GetString() ?? body;
            else
                message = body;
        }
        catch
        {
            message = string.IsNullOrWhiteSpace(body)
                ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                : body;
        }

        throw new Cli.TwCommandException(message);
    }

    public void Dispose() => _http.Dispose();
}

internal sealed record RegistryPackage(int Id, string Author, string Name, string Version, string Architecture);

internal sealed record SearchResult(IReadOnlyList<JsonElement> Items, string? NextLink);
