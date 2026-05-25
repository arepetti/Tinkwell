using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Tinkwell.Integration.Tests;

/// <summary>
/// Manages a coordinator process for integration testing. Launches the
/// real coordinator executable, captures its output, and provides pipe
/// communication for querying state.
/// </summary>
internal sealed class CoordinatorProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();
    private readonly string _pipeName;

    public string PipeName => _pipeName;
    public string StandardOutput => _stdout.ToString();
    public string StandardError => _stderr.ToString();
    public string CombinedOutput => $"{StandardOutput}\n{StandardError}";
    public bool HasExited => _process.HasExited;

    private CoordinatorProcess(Process process, string pipeName)
    {
        _process = process;
        _pipeName = pipeName;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _stderr.AppendLine(e.Data);
        };
    }

    /// <summary>
    /// Launches the coordinator process with the given <c>.tw</c> config file
    /// and optional extra command-line arguments.
    /// </summary>
    /// <param name="configPath">
    /// Absolute path to the <c>.tw</c> configuration file.
    /// </param>
    /// <param name="pipeName">
    /// Unique pipe name for this test run. Passed via
    /// <c>--Coordinator:PipeServer:PipeName</c>.
    /// </param>
    /// <param name="extraArgs">
    /// Additional command-line arguments (e.g. <c>--Coordinator:ExitAfterInit=true</c>).
    /// </param>
    public static CoordinatorProcess Start(
        string configPath,
        string pipeName,
        params string[] extraArgs)
    {
        var allArgs = new List<string>
        {
            TestPaths.CoordinatorDll,
            configPath,
            $"--Coordinator:PipeServer:PipeName={pipeName}"
        };
        allArgs.AddRange(extraArgs);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = string.Join(' ', allArgs),
            WorkingDirectory = TestPaths.ArtifactsDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var coordinator = new CoordinatorProcess(process, pipeName);

        if (!process.Start())
            throw new InvalidOperationException("Failed to start coordinator process");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return coordinator;
    }

    /// <summary>
    /// Waits for the coordinator process to exit within the given timeout.
    /// Kills the process if the timeout expires.
    /// </summary>
    /// <returns>The process exit code, or -1 if killed due to timeout.</returns>
    public async Task<int> WaitForExitAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            await _process.WaitForExitAsync(cts.Token);
            // Small delay to let async output handlers flush
            await Task.Delay(200);
            return _process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            try { _process.Kill(entireProcessTree: true); }
            catch
            {
            }
            return -1;
        }
    }

    /// <summary>
    /// Sends a command to the coordinator's named pipe and returns the raw
    /// JSON response line.
    /// </summary>
    public async Task<string> SendPipeCommandAsync(
        string command, CancellationToken cancellationToken = default)
    {
        var utf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        await using var pipe = new NamedPipeClientStream(
            ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        await pipe.ConnectAsync(10_000, cancellationToken);

        await using var writer = new StreamWriter(pipe, utf8, leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };
        using var reader = new StreamReader(pipe, utf8, leaveOpen: true);

        await writer.WriteLineAsync(command.AsMemory(), cancellationToken);
        return await reader.ReadLineAsync(cancellationToken)
            ?? throw new IOException("Empty response from coordinator pipe");
    }

    /// <summary>
    /// Sends a command and parses the JSONL envelope response.
    /// </summary>
    public async Task<JsonElement> SendCommandAsync(
        string command, CancellationToken cancellationToken = default)
    {
        var json = await SendPipeCommandAsync(command, cancellationToken);
        return JsonDocument.Parse(json).RootElement;
    }

    /// <summary>
    /// Generates a unique pipe name for test isolation.
    /// </summary>
    public static string UniquePipeName() =>
        $"tw-test-{Guid.NewGuid():N}";

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            try { _process.Kill(entireProcessTree: true); }
            catch
            {
            }

            try { await _process.WaitForExitAsync(new CancellationTokenSource(5000).Token); }
            catch
            {
            }
        }

        _process.Dispose();
    }
}
