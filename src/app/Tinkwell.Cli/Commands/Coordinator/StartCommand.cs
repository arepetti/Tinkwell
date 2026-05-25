using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Coordinator;

internal sealed class StartSettings : TwCoordinatorSettings
{
    [Description("Path to the ensemble configuration file")]
    [CommandArgument(0, "[config]")]
    public string? ConfigFile { get; set; }

    [Description("Run in the background (detach)")]
    [CommandOption("--background|-B")]
    [DefaultValue(false)]
    public bool Background { get; set; }
}

[Description("Start the coordinator")]
internal sealed class StartCommand : AsyncCommand<StartSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, StartSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        var coordinatorPath = ResolveCoordinatorPath();
        if (coordinatorPath is null)
        {
            output.WriteError("Cannot find Tinkwell.Coordinator executable next to tw");
            return 1;
        }

        var psi = new ProcessStartInfo
        {
            FileName = coordinatorPath,
            UseShellExecute = false,
            RedirectStandardOutput = settings.Background,
            RedirectStandardError = settings.Background,
            CreateNoWindow = settings.Background
        };

        if (!string.IsNullOrWhiteSpace(settings.ConfigFile))
            psi.ArgumentList.Add(settings.ConfigFile);

        psi.ArgumentList.Add($"--Coordinator:PipeServer:PipeName={settings.PipeName}");

        Process process;
        try
        {
            process = Process.Start(psi)!;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            output.WriteError($"Failed to start coordinator: {ex.Message}");
            return 1;
        }

        if (!settings.Background)
        {
            await process.WaitForExitAsync(ct);
            return process.ExitCode;
        }

        output.WriteSuccess($"Coordinator started (PID [cyan]{process.Id}[/])");
        return 0;
    }

    private static string? ResolveCoordinatorPath()
    {
        var dir = AppContext.BaseDirectory;
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        var exeName = isWindows ? "Tinkwell.Coordinator.exe" : "Tinkwell.Coordinator";
        var exePath = Path.Combine(dir, exeName);
        if (File.Exists(exePath))
            return exePath;

        var dllPath = Path.Combine(dir, "Tinkwell.Coordinator.dll");
        if (File.Exists(dllPath))
            return dllPath;

        return null;
    }
}