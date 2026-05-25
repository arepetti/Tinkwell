using Tinkwell;

namespace Tinkwell.Core.Tests;

public class TinkwellEnvironmentTests
{
    private const string WorkDir = "TINKWELL_WORKDIR";

    [Fact]
    public void DataPath_IsStableAcrossReads()
    {
        var a = TinkwellEnvironment.DataPath;
        var b = TinkwellEnvironment.DataPath;
        Assert.Equal(a, b);
        Assert.False(string.IsNullOrWhiteSpace(a));
    }

    [Fact]
    public void WorkingDirectory_UsesTinkwellWorkdir_WhenSet()
    {
        var previous = Environment.GetEnvironmentVariable(WorkDir);
        var temp = Path.Combine(Path.GetTempPath(), "tinkwell-workdir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            Environment.SetEnvironmentVariable(WorkDir, temp);
            Assert.Equal(temp, TinkwellEnvironment.WorkingDirectory);
        }
        finally
        {
            if (previous is null)
                Environment.SetEnvironmentVariable(WorkDir, null);
            else
                Environment.SetEnvironmentVariable(WorkDir, previous);
            try
            {
                Directory.Delete(temp, true);
            }
            catch
            {
                /* best effort */
            }
        }
    }

    [Fact]
    public void GetFullWorkingPath_AbsolutePath_Unchanged()
    {
        var p = Path.GetFullPath(".");
        Assert.Equal(p, TinkwellEnvironment.GetFullWorkingPath(p));
    }

    [Fact]
    public void GetFullDataPath_Relative_JoinsToDataPath()
    {
        var rel = "state/subdir";
        var full = TinkwellEnvironment.GetFullDataPath(rel);
        Assert.Equal(Path.Combine(TinkwellEnvironment.DataPath, rel), full);
    }
}
