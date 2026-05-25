using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Runner;
using Tinkwell.Runner.Hosting;

namespace Tinkwell.Runner.Hosting.Tests;

public class RunletLoaderTests
{
    [Fact]
    public void LoadAll_EmptyDescriptors_ReturnsEmpty()
    {
        var list = RunletLoader.LoadAll([], NullLogger.Instance);
        Assert.Empty(list);
    }

    [Fact]
    public void LoadAll_Throws_WhenAssemblyMissing()
    {
        var d = new RunletDescriptor("x", "nonexistent-missing-xyz.dll", new Dictionary<string, string>());
        var ex = Assert.Throws<FileNotFoundException>(() => RunletLoader.LoadAll([d], NullLogger.Instance));
        Assert.Contains("nonexistent", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
