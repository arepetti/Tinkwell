using System.Runtime.InteropServices;
using Tinkwell.Coordinator.Configuration;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Coordinator.ProcessManagement;

namespace Tinkwell.Coordinator.Tests;

public class RunnerProcessLauncherTests
{
    private static RunnerState MakeRunner(string executablePath) =>
        new(new RunnerConfig(
            "test-runner", executablePath,
            new Dictionary<string, ConfigValue>(),
            Array.Empty<RunletConfig>(),
            new SourceLocation("test.tw", 1, 1)));

    [Fact]
    public void ResolveCommand_DllPath_UsesDotnetLauncher()
    {
        var runner = MakeRunner("runners/MyRunner.dll");
        var (fileName, arguments) = RunnerProcessLauncher.ResolveCommand(runner, "test-pipe", "test-sentinel");

        Assert.Equal("dotnet", fileName);
        Assert.StartsWith("runners/MyRunner.dll", arguments);
        Assert.Contains($"--runner-id {runner.Id}", arguments);
        Assert.Contains("--coordinator-pipe test-pipe", arguments);
        Assert.Contains("--sentinel-pipe test-sentinel", arguments);
    }

    [Fact]
    public void ResolveCommand_DllPath_CaseInsensitive()
    {
        var runner = MakeRunner("runners/MyRunner.DLL");
        var (fileName, _) = RunnerProcessLauncher.ResolveCommand(runner, "pipe", "sentinel");

        Assert.Equal("dotnet", fileName);
    }

    [Fact]
    public void ResolveCommand_BarePath_UsesDirectExecutable()
    {
        var runner = MakeRunner("runners/MyRunner");
        var (fileName, arguments) = RunnerProcessLauncher.ResolveCommand(runner, "test-pipe", "test-sentinel");

        Assert.NotEqual("dotnet", fileName);
        Assert.Contains($"--runner-id {runner.Id}", arguments);
    }

    [Fact]
    public void ResolveCommand_BarePath_ArgumentsIncludePipeNames()
    {
        var runner = MakeRunner("runners/MyRunner");
        var (_, arguments) = RunnerProcessLauncher.ResolveCommand(runner, "my-pipe", "my-sentinel");

        Assert.Contains("--coordinator-pipe my-pipe", arguments);
        Assert.Contains("--sentinel-pipe my-sentinel", arguments);
    }

    [Fact]
    public void ResolveExecutablePath_OnWindows_AppendsExeIfExists()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return; // skip on non-Windows

        var tempDir = Path.Combine(Path.GetTempPath(), $"tw-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var exePath = Path.Combine(tempDir, "MyRunner.exe");
            File.WriteAllText(exePath, "");

            var result = RunnerProcessLauncher.ResolveExecutablePath(
                Path.Combine(tempDir, "MyRunner"));
            Assert.Equal(exePath, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveExecutablePath_OnWindows_FallsBackIfNoExe()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var basePath = Path.Combine(Path.GetTempPath(), "nonexistent-runner");
        var result = RunnerProcessLauncher.ResolveExecutablePath(basePath);

        Assert.Equal(basePath, result);
    }
}
