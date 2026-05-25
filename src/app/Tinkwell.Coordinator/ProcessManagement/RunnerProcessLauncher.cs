using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Tinkwell.Telemetry;

namespace Tinkwell.Coordinator.ProcessManagement;

/// <summary>
/// Launches runner processes portably across Windows, Linux, and macOS.
/// Both stdout and stderr are redirected and piped to the coordinator's
/// console so runner output always appears even with
/// <c>CreateNoWindow = true</c>.
/// </summary>
internal sealed class RunnerProcessLauncher
{
    private readonly ILogger<RunnerProcessLauncher> _logger;

    public RunnerProcessLauncher(ILogger<RunnerProcessLauncher> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Launches a runner process for the given definition.
    /// The returned <see cref="Process"/> has <see cref="Process.EnableRaisingEvents"/>
    /// set to <see langword="true"/> so callers can subscribe to <see cref="Process.Exited"/>.
    /// </summary>
    public Process Launch(RunnerState runner, string coordinatorPipeName, string sentinelPipeName)
    {
        using var activity = OtTraces.Source.Start(OtTraces.ProcessLaunch,
            (OtTraces.RunnerName, runner.Config.Name), (OtTraces.RunnerId, runner.Id));

        var (fileName, arguments) = ResolveCommand(runner, coordinatorPipeName, sentinelPipeName);

        _logger.LogDebug(
            "Starting runner '{Name}' (ID: {Id})", runner.Config.Name, runner.Id);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        if (!process.Start())
            throw new InvalidOperationException(
                $"Failed to start process for runner '{runner.Config.Name}' from '{fileName}'.");

        PipeOutput(process.StandardOutput);
        PipeOutput(process.StandardError);

        _logger.LogDebug(
            "Runner '{Name}' (ID: {Id}) started with PID {Pid}",
            runner.Config.Name, runner.Id, process.Id);

        activity?.SetTag(OtTraces.ProcessPid, process.Id);
        return process;
    }

    /// <summary>
    /// Resolves the executable path and command-line arguments for a runner.
    /// Visible for testing.
    /// </summary>
    public static (string FileName, string Arguments) ResolveCommand(
        RunnerState runner, string coordinatorPipeName, string sentinelPipeName)
    {
        var path = runner.Config.ExecutablePath;
        var runnerArgs = $"--runner-id {runner.Id} --coordinator-pipe {coordinatorPipeName} --sentinel-pipe {sentinelPipeName}";

        if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return ("dotnet", $"{path} {runnerArgs}");

        var resolvedPath = ResolveExecutablePath(path);
        return (resolvedPath, runnerArgs);
    }

    private static void PipeOutput(StreamReader reader)
    {
        _ = Task.Run(async () =>
        {
            while (await reader.ReadLineAsync() is { } line)
                Console.WriteLine(line);
        });
    }

    public static string ResolveExecutablePath(string basePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var withExe = basePath + ".exe";
            return File.Exists(withExe) ? withExe : basePath;
        }

        return basePath;
    }
}
