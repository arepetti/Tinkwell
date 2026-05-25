using Microsoft.Extensions.Logging.Abstractions;

namespace Tinkwell.Core.Tests;

public class PluginResolverTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _hostDir;

    public PluginResolverTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "tw-resolver-tests-" + Guid.NewGuid().ToString("N")[..8]);
        _hostDir = Path.Combine(_tempRoot, "host");
        Directory.CreateDirectory(_hostDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void TryLoadAssembly_NoPlugins_ReturnsNull()
    {
        var pluginsDir = Path.Combine(_tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var catalog = new PluginCatalog([pluginsDir]);
        var resolver = new PluginResolver(catalog, _hostDir, NullLogger.Instance);

        Assert.Null(resolver.TryLoadAssembly("Nonexistent.dll"));
    }

    [Fact]
    public void TryLoadAssembly_FileNotFound_ReturnsNull()
    {
        var pluginsDir = Path.Combine(_tempRoot, "plugins");
        var pluginDir = Path.Combine(pluginsDir, "test@1.0.0");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllBytes(Path.Combine(pluginDir, "Decoy.dll"), [0]);

        var catalog = new PluginCatalog([pluginsDir]);
        var resolver = new PluginResolver(catalog, _hostDir, NullLogger.Instance);

        Assert.Null(resolver.TryLoadAssembly("Missing.dll"));
    }

    [Fact]
    public void TryLoadAssembly_PicksHighestVersion()
    {
        var pluginsDir = Path.Combine(_tempRoot, "plugins");

        var v1Dir = Path.Combine(pluginsDir, "test@1.0.0");
        var v2Dir = Path.Combine(pluginsDir, "test@2.0.0");
        Directory.CreateDirectory(v1Dir);
        Directory.CreateDirectory(v2Dir);

        File.WriteAllBytes(Path.Combine(v1Dir, "Marker.dll"), [0]);
        File.WriteAllBytes(Path.Combine(v2Dir, "Marker.dll"), [0]);

        var catalog = new PluginCatalog([pluginsDir]);

        var entry = catalog.Resolve("Marker.dll");
        Assert.NotNull(entry);
        Assert.Equal(new Version(2, 0, 0), entry.Version);
        Assert.Equal(v2Dir, entry.Directory);
    }

    [Fact]
    public void PluginEntry_PreservesAllProperties()
    {
        var entry = new PluginEntry(
            "my-plugin",
            new Version(1, 2, 3),
            "/some/dir",
            ["A.dll", "B.dll"],
            0);

        Assert.Equal("my-plugin", entry.Name);
        Assert.Equal(new Version(1, 2, 3), entry.Version);
        Assert.Equal("/some/dir", entry.Directory);
        Assert.Equal(2, entry.Assemblies.Count);
        Assert.Equal(0, entry.SourcePriority);
    }
}
