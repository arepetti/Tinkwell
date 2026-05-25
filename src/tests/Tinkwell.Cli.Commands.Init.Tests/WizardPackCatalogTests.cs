using Tinkwell.Cli.Commands.Init;

namespace Tinkwell.Cli.Commands.Init.Tests;

public class WizardPackCatalogTests
{
    [Fact]
    public void FindPackDirectory_ReturnsNullForNonexistent()
    {
        var catalog = new WizardPackCatalog();
        Assert.Null(catalog.FindPackDirectory("nonexistent-pack-xyz"));
    }

    [Fact]
    public void DiscoverPackDirectories_WithExplicitPath_FindsPacks()
    {
        var packRoot = FindInitRoot();
        if (packRoot is null)
            return; // skip if packs not in output

        var catalog = new WizardPackCatalog(packRoot);
        var dirs = catalog.DiscoverPackDirectories();

        Assert.Contains(dirs, d => Path.GetFileName(d) == "tinkwell-ensemble");
    }

    private static string? FindInitRoot()
    {
        var dir = AppContext.BaseDirectory;
        var candidate = Path.Combine(dir, "packs", "init");
        if (Directory.Exists(candidate))
            return candidate;

        var current = new DirectoryInfo(dir);
        while (current is not null)
        {
            candidate = Path.Combine(current.FullName, "src", "Tinkwell.Cli.Commands.Init",
                "packs", "init");
            if (Directory.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        return null;
    }
}
