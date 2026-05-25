namespace Tinkwell.Core.Tests;

public class PluginCatalogTests : IDisposable
{
    private readonly string _tempRoot;

    public PluginCatalogTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "tw-plugin-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
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

    // -- TryParseDirectoryName --

    [Theory]
    [InlineData("my-runlet-json@1.0.0", "my-runlet-json", "1.0.0")]
    [InlineData("fw@2.3.1", "fw", "2.3.1")]
    [InlineData("org@plugin@0.1.0", "org@plugin", "0.1.0")]
    public void ParseDirectoryName_ValidFormats(string dirName, string expectedName, string expectedVersion)
    {
        var result = PluginCatalog.TryParseDirectoryName(dirName, out var name, out var version);
        Assert.True(result);
        Assert.Equal(expectedName, name);
        Assert.Equal(new Version(expectedVersion), version);
    }

    [Theory]
    [InlineData("no-version")]
    [InlineData("@1.0.0")]
    [InlineData("name@invalid")]
    [InlineData("")]
    public void ParseDirectoryName_InvalidFormats(string dirName)
    {
        Assert.False(PluginCatalog.TryParseDirectoryName(dirName, out _, out _));
    }

    // -- Scan and Plugins --

    [Fact]
    public void EmptyRoot_ReturnsNoPlugins()
    {
        var catalog = new PluginCatalog([_tempRoot]);
        Assert.Empty(catalog.Plugins);
    }

    [Fact]
    public void SinglePlugin_Discovered()
    {
        CreatePlugin(_tempRoot, "my-runlet@1.0.0", "My.Runlet.dll");

        var catalog = new PluginCatalog([_tempRoot]);
        var plugin = Assert.Single(catalog.Plugins);
        Assert.Equal("my-runlet", plugin.Name);
        Assert.Equal(new Version(1, 0, 0), plugin.Version);
        Assert.Contains("My.Runlet.dll", plugin.Assemblies);
    }

    [Fact]
    public void MultipleVersions_SortedDescending()
    {
        CreatePlugin(_tempRoot, "sensor@1.0.0", "Sensor.dll");
        CreatePlugin(_tempRoot, "sensor@2.0.0", "Sensor.dll");
        CreatePlugin(_tempRoot, "sensor@1.5.0", "Sensor.dll");

        var catalog = new PluginCatalog([_tempRoot]);
        Assert.Equal(3, catalog.Plugins.Count);
        Assert.Equal(new Version(2, 0, 0), catalog.Plugins[0].Version);
        Assert.Equal(new Version(1, 5, 0), catalog.Plugins[1].Version);
        Assert.Equal(new Version(1, 0, 0), catalog.Plugins[2].Version);
    }

    [Fact]
    public void DirectoryWithoutAt_Skipped()
    {
        var dir = Path.Combine(_tempRoot, "no-version-dir");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "Something.dll"), [0]);

        var catalog = new PluginCatalog([_tempRoot]);
        Assert.Empty(catalog.Plugins);
    }

    [Fact]
    public void DirectoryWithNoDlls_Skipped()
    {
        var dir = Path.Combine(_tempRoot, "empty@1.0.0");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "readme.txt"), "hi");

        var catalog = new PluginCatalog([_tempRoot]);
        Assert.Empty(catalog.Plugins);
    }

    [Fact]
    public void MultiplePluginNames_SortedByNameThenVersion()
    {
        CreatePlugin(_tempRoot, "alpha@1.0.0", "Alpha.dll");
        CreatePlugin(_tempRoot, "beta@2.0.0", "Beta.dll");
        CreatePlugin(_tempRoot, "alpha@2.0.0", "Alpha.dll");

        var catalog = new PluginCatalog([_tempRoot]);
        Assert.Equal(3, catalog.Plugins.Count);
        Assert.Equal("alpha", catalog.Plugins[0].Name);
        Assert.Equal(new Version(2, 0, 0), catalog.Plugins[0].Version);
        Assert.Equal("alpha", catalog.Plugins[1].Name);
        Assert.Equal(new Version(1, 0, 0), catalog.Plugins[1].Version);
        Assert.Equal("beta", catalog.Plugins[2].Name);
    }

    // -- Resolve by assembly filename --

    [Fact]
    public void ResolveByAssembly_ReturnsHighestVersion()
    {
        CreatePlugin(_tempRoot, "runlet@1.0.0", "My.Runlet.dll");
        CreatePlugin(_tempRoot, "runlet@3.0.0", "My.Runlet.dll");
        CreatePlugin(_tempRoot, "runlet@2.0.0", "My.Runlet.dll");

        var catalog = new PluginCatalog([_tempRoot]);
        var entry = catalog.Resolve("My.Runlet.dll");
        Assert.NotNull(entry);
        Assert.Equal(new Version(3, 0, 0), entry.Version);
    }

    [Fact]
    public void ResolveByAssembly_CaseInsensitive()
    {
        CreatePlugin(_tempRoot, "runlet@1.0.0", "My.Runlet.dll");

        var catalog = new PluginCatalog([_tempRoot]);
        Assert.NotNull(catalog.Resolve("my.runlet.dll"));
        Assert.NotNull(catalog.Resolve("MY.RUNLET.DLL"));
    }

    [Fact]
    public void ResolveByAssembly_NotFound_ReturnsNull()
    {
        CreatePlugin(_tempRoot, "runlet@1.0.0", "Other.dll");

        var catalog = new PluginCatalog([_tempRoot]);
        Assert.Null(catalog.Resolve("Missing.dll"));
    }

    // -- Resolve by plugin name --

    [Fact]
    public void ResolveByName_ReturnsHighestVersion()
    {
        CreatePlugin(_tempRoot, "my-runlet@1.0.0", "A.dll");
        CreatePlugin(_tempRoot, "my-runlet@2.5.0", "A.dll");

        var catalog = new PluginCatalog([_tempRoot]);
        var entry = catalog.Resolve("my-runlet", minVersion: null);
        Assert.NotNull(entry);
        Assert.Equal(new Version(2, 5, 0), entry.Version);
    }

    [Fact]
    public void ResolveByName_WithMinVersion()
    {
        CreatePlugin(_tempRoot, "sensor@1.0.0", "S.dll");
        CreatePlugin(_tempRoot, "sensor@2.0.0", "S.dll");
        CreatePlugin(_tempRoot, "sensor@3.0.0", "S.dll");

        var catalog = new PluginCatalog([_tempRoot]);
        var entry = catalog.Resolve("sensor", new Version(2, 0, 0));
        Assert.NotNull(entry);
        Assert.Equal(new Version(3, 0, 0), entry.Version);
    }

    [Fact]
    public void ResolveByName_MinVersionExcludesAll_ReturnsNull()
    {
        CreatePlugin(_tempRoot, "sensor@1.0.0", "S.dll");

        var catalog = new PluginCatalog([_tempRoot]);
        Assert.Null(catalog.Resolve("sensor", new Version(5, 0, 0)));
    }

    // -- Multi-source priority --

    [Fact]
    public void MultiSource_SameNameVersion_HigherPriorityWins()
    {
        var source1 = Path.Combine(_tempRoot, "high");
        var source2 = Path.Combine(_tempRoot, "low");
        CreatePlugin(source1, "runlet@1.0.0", "R.dll", "Helper1.dll");
        CreatePlugin(source2, "runlet@1.0.0", "R.dll", "Helper2.dll");

        var catalog = new PluginCatalog([source1, source2]);
        var entry = catalog.Resolve("R.dll");
        Assert.NotNull(entry);
        Assert.Equal(0, entry.SourcePriority);
        Assert.Contains("Helper1.dll", entry.Assemblies);
    }

    [Fact]
    public void MultiSource_DifferentVersions_HighestVersionWins()
    {
        var source1 = Path.Combine(_tempRoot, "high");
        var source2 = Path.Combine(_tempRoot, "low");
        CreatePlugin(source1, "runlet@1.0.0", "R.dll");
        CreatePlugin(source2, "runlet@2.0.0", "R.dll");

        var catalog = new PluginCatalog([source1, source2]);
        var entry = catalog.Resolve("R.dll");
        Assert.NotNull(entry);
        Assert.Equal(new Version(2, 0, 0), entry.Version);
        Assert.Equal(1, entry.SourcePriority);
    }

    [Fact]
    public void MultiSource_VersionTie_HigherPrioritySourceWins()
    {
        var source1 = Path.Combine(_tempRoot, "env");
        var source2 = Path.Combine(_tempRoot, "user");
        var source3 = Path.Combine(_tempRoot, "appdata");
        CreatePlugin(source2, "my-plugin@1.0.0", "P.dll");
        CreatePlugin(source3, "my-plugin@1.0.0", "P.dll");

        var catalog = new PluginCatalog([source1, source2, source3]);
        var entry = catalog.Resolve("P.dll");
        Assert.NotNull(entry);
        Assert.Equal(1, entry.SourcePriority); // source2 = priority 1
    }

    [Fact]
    public void MultiSource_NonExistentDirectories_Skipped()
    {
        CreatePlugin(_tempRoot, "runlet@1.0.0", "R.dll");

        var catalog = new PluginCatalog([
            Path.Combine(_tempRoot, "nonexistent1"),
            _tempRoot,
            Path.Combine(_tempRoot, "nonexistent2"),
        ]);

        var entry = Assert.Single(catalog.Plugins);
        Assert.Equal("runlet", entry.Name);
        Assert.Equal(1, entry.SourcePriority); // _tempRoot is index 1
    }

    [Fact]
    public void MultiSource_MixedPlugins_AllDiscovered()
    {
        var source1 = Path.Combine(_tempRoot, "s1");
        var source2 = Path.Combine(_tempRoot, "s2");
        CreatePlugin(source1, "alpha@1.0.0", "Alpha.dll");
        CreatePlugin(source2, "beta@1.0.0", "Beta.dll");

        var catalog = new PluginCatalog([source1, source2]);
        Assert.Equal(2, catalog.Plugins.Count);
        Assert.NotNull(catalog.Resolve("Alpha.dll"));
        Assert.NotNull(catalog.Resolve("Beta.dll"));
    }

    // -- Rescan --

    [Fact]
    public void Scan_PicksUpNewPlugins()
    {
        var catalog = new PluginCatalog([_tempRoot]);
        Assert.Empty(catalog.Plugins);

        CreatePlugin(_tempRoot, "new-plugin@1.0.0", "New.dll");
        catalog.Scan();

        Assert.Single(catalog.Plugins);
    }

    // -- Helpers --

    private static void CreatePlugin(string root, string dirName, params string[] dllNames)
    {
        var dir = Path.Combine(root, dirName);
        Directory.CreateDirectory(dir);
        foreach (var dll in dllNames)
            File.WriteAllBytes(Path.Combine(dir, dll), [0]);
    }
}
