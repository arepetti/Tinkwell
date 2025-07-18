﻿using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Supervisor.Sentinel;

sealed class SentinelProcessBuilder(ILogger<SentinelProcessBuilder> logger) : IChildProcessBuilder
{
    public IChildProcess Create(RunnerDefinition definition)
    {
        // Ideally we'd want ShellExecute=true but it's only for Windows; we could check for
        // !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) to use the "open" command but it doesn't
        // work for all executables on MacOS. On the top of that we can't add environment variables with
        // ShellExecute=true (we need them to inform the child about its name and the supervisor PID in
        // an easy and portable way).
        var psi = new ProcessStartInfo()
        {
            FileName = ResolveFileName(definition.Path),
            Arguments = definition.Arguments,
            WorkingDirectory = ResolveWorkingDirectory(definition.Path),
        };
      
        psi.EnvironmentVariables[WellKnownNames.RunnerNameEnvironmentVariable] = definition.Name;
        psi.EnvironmentVariables[WellKnownNames.SupervisorPidEnvironmentVariable] = Environment.ProcessId.ToString();

        if (definition.ShouldKeepAlive())
            return new SupervisionedChildProcess(_logger, psi, definition);

        return new ChildProcess(_logger, psi, definition);

        static string ResolveWorkingDirectory(string executablePath)
        {
            // We set the working directory to the directory of the executable if the
            // path is absolute (like for a program installed in a specific location) but
            // use the current directory for everything else.
            if (Path.IsPathFullyQualified(executablePath))
                return Path.GetDirectoryName(executablePath)!;

            return Environment.CurrentDirectory;
        }
    }

    private readonly ILogger<SentinelProcessBuilder> _logger = logger;

    private static string ResolveFileName(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // If the path is not absolute, we need to append the .exe extension so that we do not need
            // to worry, inside an Ensamble file, about the OS.
            // In future we might handle here automatic hosting for DLL files (or dotnet run) but for now
            // let's keep it quirky but simple.
            if (!Path.IsPathRooted(path) && !Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                return path + ".exe";
        }

        return path;
    }
}
