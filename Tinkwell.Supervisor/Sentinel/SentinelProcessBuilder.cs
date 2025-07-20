using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Supervisor.Sentinel;

sealed class SentinelProcessBuilder : IChildProcessBuilder
{
    public SentinelProcessBuilder(ILogger<SentinelProcessBuilder> logger)
    {
        _logger = logger;
        _logger.LogInformation("Working directory: {Path}", HostingInformation.WorkingDirectory);
        _logger.LogInformation("Current directory: {Path}", Environment.CurrentDirectory);
        _logger.LogInformation("Executables directory: {Path}", StrategyAssemblyLoader.GetAppPath());
    }

    public IChildProcess Create(RunnerDefinition definition)
    {
        _logger.LogDebug("Creating process for {Path}", definition.Path);
        var psi = CreatePsi(definition);

        psi.EnvironmentVariables[WellKnownNames.RunnerNameEnvironmentVariable] = definition.Name;
        psi.EnvironmentVariables[WellKnownNames.SupervisorPidEnvironmentVariable] = Environment.ProcessId.ToString();

        if (definition.ShouldKeepAlive())
            return new SupervisionedChildProcess(_logger, psi, definition);

        return new ChildProcess(_logger, psi, definition);
    }

    private readonly ILogger<SentinelProcessBuilder> _logger;

    private static ProcessStartInfo CreatePsi(RunnerDefinition definition)
    {
        string fileName = definition.Path;
        string arguments = definition.Arguments ?? "";

        string? dotNetExecutable = GetDotNetExecutable();
        if (dotNetExecutable is not null)
        {
            fileName = "dotnet";
            arguments = $"\"{dotNetExecutable}\" {arguments}";
        }

        return new ProcessStartInfo()
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = ResolveWorkingDirectory(fileName),
        };

        string? GetDotNetExecutable()
        {
            // We have to use this quirky "algorithm" because of compatibility
            // with the earlier definitions where a .NET executable path to run with
            // dotnent command could have been:
            //   Tinkwell.Bootstrapper.DllHost => Yes
            //   Tinkwell.Bootstrapper.DllHost.exe => No 
            //   ./Tinkwell.Bootstrapper.DllHost => Yes
            //   /Path/To/File.exe => No
            //   /Path/To/File.dll => Yes
            if (Path.GetExtension(definition.Path) == ".dll")
                return definition.Path;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Path.GetExtension(definition.Path) == ".exe")
                return null;

            if (File.Exists(definition.Path))
                return null;

            if (Path.IsPathRooted(definition.Path))
                return File.Exists(definition.Path + ".dll") ? definition.Path + ".dll" : null;

            string directory = StrategyAssemblyLoader.GetAppPath();
            string altPath = Path.Combine(directory, definition.Path) + ".dll";
            return File.Exists(altPath) ? altPath : null;
        }

        static string ResolveWorkingDirectory(string executablePath)
        {
            // We set the working directory to the directory of the executable if the
            // path is absolute (like for a program installed in a specific location) but
            // use the working directory for everything else.
            if (Path.IsPathFullyQualified(executablePath))
                return Path.GetDirectoryName(executablePath)!;
            
            // Note that working directory != current directory
            // The working directory is where we load data files from, the current directory
            // is where we load executables (if a path is not specified).
            return HostingInformation.WorkingDirectory;
        }
    }
}
