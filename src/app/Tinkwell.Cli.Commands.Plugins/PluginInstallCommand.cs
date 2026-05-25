using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using NuGet.Versioning;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Package;

namespace Tinkwell.Cli.Commands.Plugins;

public sealed class PluginInstallSettings : TwSettings
{
    [Description("Package source: file path, URL, or registry name (handle/plugin-name or handle/plugin-name@version)")]
    [CommandArgument(0, "<source>")]
    public string Source { get; set; } = "";

    [Description("Path to a publisher public key file. May be specified multiple times to trust several publishers.")]
    [CommandOption("--key|-k")]
    public string[] KeyFiles { get; set; } = Array.Empty<string>();

    [Description("Overwrite if the same version is already installed")]
    [CommandOption("--force")]
    [DefaultValue(false)]
    public bool Force { get; set; }

    [Description("Remove older versions of the same plugin after installing")]
    [CommandOption("--update")]
    [DefaultValue(false)]
    public bool Update { get; set; }

    [Description("Allow packages without signatures")]
    [CommandOption("--allow-unsigned")]
    [DefaultValue(false)]
    public bool AllowUnsigned { get; set; }

    [Description("Accept integrity-only verification when no --key is supplied and no registry key is available. Insecure: cannot detect forged signature manifests.")]
    [CommandOption("--allow-integrity-only")]
    [DefaultValue(false)]
    public bool AllowIntegrityOnly { get; set; }

    [Description("Plugin registry URL (overrides TW_REGISTRY_URL and config)")]
    [CommandOption("--registry-url")]
    public string? RegistryUrl { get; set; }

    [Description("Path to the registry's public key file (overrides TW_REGISTRY_PUBLIC_KEY_FILE and config)")]
    [CommandOption("--registry-key")]
    public string? RegistryKeyFile { get; set; }

    [Description("HTTP timeout in seconds for URL/registry downloads (default: 60)")]
    [CommandOption("--timeout")]
    [DefaultValue(60)]
    public int Timeout { get; set; } = 60;

    [Description("GitHub repository for fallback plugin downloads (overrides TW_GITHUB_REPO and config)")]
    [CommandOption("--github-repo")]
    public string? GitHubRepo { get; set; }

    internal PluginSourceInfo? RegistrySource { get; set; }
    internal GitHubSourceInfo? GitHubSource { get; set; }
    internal byte[]? AutoFetchedPublicKey { get; set; }
}

internal sealed record PluginSourceInfo(string RegistryUrl, string Handle, string Name);
internal sealed record GitHubSourceInfo(string Repo, string PluginName);

[CliCommand("plugin", "install", Description = "Install a plugin from a package file or URL")]
public sealed class PluginInstallCommand : AsyncCommand<PluginInstallSettings>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context, PluginInstallSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var source = settings.Source;
            string zipPath;
            string? tempDownload = null;

            if (IsUrl(source))
            {
                tempDownload = Path.Combine(Path.GetTempPath(),
                    "tw-plugin-dl-" + Guid.NewGuid().ToString("N")[..8] + ".zip");

                await output.RunWithStatusAsync("Downloading...", async () =>
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(settings.Timeout) };
                    using var response = await http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, ct);
                    response.EnsureSuccessStatusCode();
                    using var fs = File.Create(tempDownload);
                    await response.Content.CopyToAsync(fs, ct);
                });

                zipPath = tempDownload;
            }
            else if (File.Exists(source))
            {
                zipPath = Path.GetFullPath(source);
            }
            else if (TryParseRegistryName(source, out var handle, out var pluginName, out var version))
            {
                tempDownload = Path.Combine(Path.GetTempPath(),
                    "tw-plugin-dl-" + Guid.NewGuid().ToString("N")[..8] + ".zip");

                var registryUrl = RegistryConfig.ResolveUrl(settings.RegistryUrl);
                bool downloadedFromRegistry = false;

                if (!string.IsNullOrWhiteSpace(registryUrl))
                {
                    var registryKeyFile = RegistryConfig.ResolvePublicKeyFile(settings.RegistryKeyFile);
                    if (registryKeyFile is not null && settings.KeyFiles.Length == 0)
                        settings.KeyFiles = new[] { registryKeyFile };

                    settings.RegistrySource = new PluginSourceInfo(registryUrl, handle, pluginName);

                    using var registry = new PluginRegistryClient(registryUrl);

                    if (settings.KeyFiles.Length == 0)
                    {
                        settings.AutoFetchedPublicKey = await registry.FetchRegistryPublicKeyAsync(ct);
                    }

                    var package = await output.RunWithStatusAsync(
                        $"Resolving {handle}/{pluginName}" + (version is not null ? $"@{version}" : "") + "...",
                        () => registry.ResolvePackageAsync(handle, pluginName, version, ct));

                    if (package is not null)
                    {
                        if (!settings.NonInteractive && output.Format != OutputFormat.Jsonl)
                            output.WriteMarkup(
                                $"  Found [bold]{package.Author}/{package.Name}[/] v{package.Version} ({package.Architecture})");

                        await output.RunWithStatusAsync("Downloading from registry...", async () =>
                        {
                            using var stream = await registry.DownloadAsync(package.Id, ct);
                            using var fs = File.Create(tempDownload);
                            await stream.CopyToAsync(fs, ct);
                        });

                        downloadedFromRegistry = true;
                    }
                }

                if (!downloadedFromRegistry)
                {
                    var githubRepo = RegistryConfig.ResolveGitHubRepo(settings.GitHubRepo);

                    if (!settings.NonInteractive && output.Format != OutputFormat.Jsonl)
                    {
                        if (!string.IsNullOrWhiteSpace(registryUrl))
                            output.WriteMarkup("  Not found in registry, trying GitHub releases...");
                        else
                            output.WriteMarkup($"  No registry configured, trying GitHub releases ({githubRepo})...");
                    }

                    using var github = new GitHubReleasesClient(githubRepo);
                    var asset = await output.RunWithStatusAsync(
                        $"Resolving {pluginName}" + (version is not null ? $"@{version}" : "") + " on GitHub...",
                        () => github.ResolveAsync(pluginName, version, ct));

                    if (asset is null)
                        return WriteError(output,
                            $"Package '{source}' not found" +
                            (!string.IsNullOrWhiteSpace(registryUrl)
                                ? " in the registry or GitHub releases."
                                : $" in GitHub releases ({githubRepo})."));

                    if (!settings.NonInteractive && output.Format != OutputFormat.Jsonl)
                        output.WriteMarkup(
                            $"  Found [bold]{asset.Name}[/] v{asset.Version} on GitHub");

                    settings.GitHubSource = new GitHubSourceInfo(githubRepo, pluginName);

                    await output.RunWithStatusAsync("Downloading from GitHub...", async () =>
                    {
                        using var stream = await github.DownloadAssetAsync(asset.DownloadUrl, ct);
                        using var fs = File.Create(tempDownload);
                        await stream.CopyToAsync(fs, ct);
                    });
                }

                zipPath = tempDownload;
            }
            else if (!source.Contains(Path.DirectorySeparatorChar) &&
                     !source.Contains(Path.AltDirectorySeparatorChar) &&
                     !source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return WriteError(output,
                    $"Unknown source '{source}'. Use a file path, URL, or registry name (handle/plugin-name[@version]).");
            }
            else
            {
                return WriteError(output, $"File not found: {source}");
            }

            try
            {
                return await InstallFromZipAsync(zipPath, settings, output, ct);
            }
            finally
            {
                if (tempDownload is not null && File.Exists(tempDownload))
                    File.Delete(tempDownload);
            }
        }
        catch (TwCommandException ex)
        {
            return WriteError(output, ex.Message);
        }
        catch (PackageException ex)
        {
            return WriteError(output, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return WriteError(output, $"Download failed: {ex.Message}");
        }
    }

    private async Task<int> InstallFromZipAsync(
        string zipPath, PluginInstallSettings settings, OutputContext output, CancellationToken ct)
    {
        var trustedKeys = new List<byte[]>(settings.KeyFiles.Length + 1);
        foreach (var keyFile in settings.KeyFiles)
            trustedKeys.Add(await File.ReadAllBytesAsync(keyFile, ct));
        if (trustedKeys.Count == 0 && settings.AutoFetchedPublicKey is not null)
            trustedKeys.Add(settings.AutoFetchedPublicKey);

        if (trustedKeys.Count == 0 && !settings.AllowUnsigned && !settings.AllowIntegrityOnly)
            return WriteError(output,
                "No publisher key supplied and no registry key available. " +
                "Pass --key <path> (once per trusted publisher), " +
                "or --allow-integrity-only to install without verifying the signature, " +
                "or --allow-unsigned for unsigned packages.");

        var unpackOptions = new UnpackOptions
        {
            Verify = true,
            TrustedKeys = trustedKeys,
            RequireSignatures = !settings.AllowUnsigned,
            AllowIntegrityOnly = settings.AllowIntegrityOnly,
        };

        var tempDir = Path.Combine(Path.GetTempPath(),
            "tw-plugin-inst-" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            var packer = new TwPackage();
            await output.RunWithStatusAsync("Verifying and extracting...", () =>
                packer.UnpackAsync(zipPath, tempDir, unpackOptions, ct));

            var manifestPath = Path.Combine(tempDir, WellKnownPaths.Manifest);
            if (!File.Exists(manifestPath))
                throw new TwCommandException("Package does not contain package.tw");

            var manifestText = await File.ReadAllTextAsync(manifestPath, ct);
            var manifest = ManifestFormat.Parse(manifestText);

            if (string.IsNullOrWhiteSpace(manifest.Version))
                throw new TwCommandException("package.tw must specify a 'version' for plugin installation");

            var compatResult = CheckProductVersionCompatibility(manifest, settings.Force);
            if (compatResult is { IsCompatible: false, Forced: false })
                return WriteError(output, compatResult.Message);
            if (compatResult is { Forced: true })
                output.WriteWarning(compatResult.Message);

            var pluginsRoot = GetInstallRoot();
            Directory.CreateDirectory(pluginsRoot);

            var targetDir = Path.Combine(pluginsRoot, $"{manifest.Name}@{manifest.Version}");

            if (Directory.Exists(targetDir) && !settings.Force)
            {
                if (output.Format == OutputFormat.Jsonl)
                {
                    var json = JsonSerializer.Serialize(new
                    {
                        status = "ok",
                        name = manifest.Name,
                        version = manifest.Version,
                        message = $"Already installed: {manifest.Name}@{manifest.Version} (use --force to overwrite)",
                        skipped = true,
                    }, JsonOpts);
                    Console.WriteLine(json);
                }
                else
                {
                    output.WriteWarning(
                        $"[bold]{manifest.Name}@{manifest.Version}[/] is already installed. Use [bold]--force[/] to overwrite.");
                }
                return 0;
            }

            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, recursive: true);

            Directory.CreateDirectory(targetDir);

            var contentDir = Path.Combine(tempDir, WellKnownPaths.ContentDirectory);
            if (Directory.Exists(contentDir))
                CopyDirectory(contentDir, targetDir);

            File.Copy(manifestPath, Path.Combine(targetDir, WellKnownPaths.Manifest));

            if (settings.RegistrySource is not null)
                WritePluginSourceMetadata(targetDir, settings.RegistrySource);
            else if (settings.GitHubSource is not null)
                WriteGitHubSourceMetadata(targetDir, settings.GitHubSource);

            int removedCount = 0;
            if (settings.Update)
                removedCount = RemoveOtherVersions(pluginsRoot, manifest.Name, manifest.Version);

            if (output.Format == OutputFormat.Jsonl)
            {
                var json = JsonSerializer.Serialize(new
                {
                    status = "ok",
                    name = manifest.Name,
                    version = manifest.Version,
                    message = $"Installed {manifest.Name}@{manifest.Version}",
                    oldVersionsRemoved = removedCount,
                    warning = compatResult?.Message,
                }, JsonOpts);
                Console.WriteLine(json);
            }
            else
            {
                output.WriteSuccess($"Installed [bold]{manifest.Name}@{manifest.Version}[/]");
                if (removedCount > 0)
                    output.WriteMarkup($"  Removed {removedCount} older version(s)");
            }

            return 0;
        }
        finally
        {
            if (Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, recursive: true); }
                catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"Failed to clean up temp directory {tempDir}: {ex.Message}");
                }
        }
    }

    private static CompatCheckResult? CheckProductVersionCompatibility(
        PackageManifest manifest, bool force)
    {
        if (!manifest.Properties.TryGetValue("product-version", out var rangeStr)
            || string.IsNullOrWhiteSpace(rangeStr))
            return null;

        if (!VersionRange.TryParse(rangeStr, out var range))
            return new(false, false,
                $"Invalid product-version range '{rangeStr}' in package.tw.");

        var infoVersion = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (infoVersion is null)
            return null;

        var plusIndex = infoVersion.IndexOf('+');
        if (plusIndex >= 0)
            infoVersion = infoVersion[..plusIndex];

        if (!NuGetVersion.TryParse(infoVersion, out var currentVersion))
            return null;

        if (range.Satisfies(currentVersion))
            return null;

        var message =
            $"Plugin requires Tinkwell {rangeStr} but the current version is {currentVersion}.";

        return force
            ? new(true, true, message + " Installing anyway (--force).")
            : new(false, false, message + " Use --force to install anyway.");
    }

    private sealed record CompatCheckResult(bool IsCompatible, bool Forced, string Message);

    private static int RemoveOtherVersions(string pluginsRoot, string pluginName, string keepVersion)
    {
        int removed = 0;
        foreach (var dir in Directory.EnumerateDirectories(pluginsRoot))
        {
            var dirName = Path.GetFileName(dir);
            if (!PluginCatalog.TryParseDirectoryName(dirName, out var name, out var version))
                continue;

            if (!name.Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (version.ToString() == keepVersion)
                continue;

            Directory.Delete(dir, recursive: true);
            removed++;
        }
        return removed;
    }

    internal static string GetInstallRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Tinkwell", "plugins");
    }

    private static int WriteError(OutputContext output, string message)
    {
        output.WriteError(message);
        return 1;
    }

    /// <summary>
    /// Detects registry package names in the format "handle/plugin-name" or "handle/plugin-name@version".
    /// </summary>
    internal static bool TryParseRegistryName(
        string source, out string handle, out string pluginName, out string? version)
    {
        handle = "";
        pluginName = "";
        version = null;

        if (source.Contains(Path.DirectorySeparatorChar) && Path.DirectorySeparatorChar != '/')
            return false;
        if (source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return false;
        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        var slashIndex = source.IndexOf('/');
        if (slashIndex <= 0 || slashIndex >= source.Length - 1)
            return false;

        if (source.IndexOf('/', slashIndex + 1) >= 0)
            return false;

        handle = source[..slashIndex];
        var rest = source[(slashIndex + 1)..];

        var atIndex = rest.LastIndexOf('@');
        if (atIndex > 0 && atIndex < rest.Length - 1)
        {
            pluginName = rest[..atIndex];
            version = rest[(atIndex + 1)..];
        }
        else
        {
            pluginName = rest;
        }

        return pluginName.Length > 0 && handle.Length > 0;
    }

    private static bool IsUrl(string source) =>
        source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    internal const string PluginSourceFile = ".plugin-source.json";

    private static void WritePluginSourceMetadata(string pluginDir, PluginSourceInfo source)
    {
        var data = new { registryUrl = source.RegistryUrl, handle = source.Handle, name = source.Name, installedFrom = "registry" };
        var json = JsonSerializer.Serialize(data, JsonOpts);
        File.WriteAllText(Path.Combine(pluginDir, PluginSourceFile), json);
    }

    internal static PluginSourceInfo? ReadPluginSourceMetadata(string pluginDir)
    {
        var path = Path.Combine(pluginDir, PluginSourceFile);
        if (!File.Exists(path))
            return null;
        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (ReadInstalledFrom(root) != "registry")
                return null;
            var url = root.GetProperty("registryUrl").GetString();
            var handle = root.GetProperty("handle").GetString();
            var name = root.GetProperty("name").GetString();
            if (url is not null && handle is not null && name is not null)
                return new PluginSourceInfo(url, handle, name);
        }
        catch
        {
        }
        return null;
    }

    private static void WriteGitHubSourceMetadata(string pluginDir, GitHubSourceInfo source)
    {
        var data = new { githubRepo = source.Repo, name = source.PluginName, installedFrom = "github" };
        var json = JsonSerializer.Serialize(data, JsonOpts);
        File.WriteAllText(Path.Combine(pluginDir, PluginSourceFile), json);
    }

    internal static GitHubSourceInfo? ReadGitHubSourceMetadata(string pluginDir)
    {
        var path = Path.Combine(pluginDir, PluginSourceFile);
        if (!File.Exists(path))
            return null;
        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (ReadInstalledFrom(root) != "github")
                return null;
            var repo = root.GetProperty("githubRepo").GetString();
            var name = root.GetProperty("name").GetString();
            if (repo is not null && name is not null)
                return new GitHubSourceInfo(repo, name);
        }
        catch
        {
        }
        return null;
    }

    internal static string? ReadInstalledFrom(string pluginDir)
    {
        var path = Path.Combine(pluginDir, PluginSourceFile);
        if (!File.Exists(path))
            return null;
        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(path));
            return ReadInstalledFrom(doc.RootElement);
        }
        catch
        {
        }
        return null;
    }

    private static string? ReadInstalledFrom(JsonElement root)
    {
        return root.TryGetProperty("installedFrom", out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var dir in Directory.EnumerateDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }
}