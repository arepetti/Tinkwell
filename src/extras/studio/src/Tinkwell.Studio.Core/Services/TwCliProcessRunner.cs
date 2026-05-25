using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Studio.Services;

/// <summary>
/// Invokes `tw` as a child process. Always appends `--format jsonl --non-interactive` so
/// that stdout is a stream of JSON objects (one per line) with no Spectre markup.
/// </summary>
public sealed class TwCliProcessRunner : ITwCli
{
    private readonly StudioSettings _settings;
    private readonly ILogger<TwCliProcessRunner> _logger;
    private readonly CommandLog? _commandLog;

    public TwCliProcessRunner(
        StudioSettings settings,
        ILogger<TwCliProcessRunner> logger,
        CommandLog? commandLog = null)
    {
        _settings = settings;
        _logger = logger;
        _commandLog = commandLog;
    }

    public async Task<JsonElement> RunOneShotAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default)
    {
        var many = await RunOneShotManyAsync(args, cancellationToken).ConfigureAwait(false);
        return many.Count == 0 ? default : many[0];
    }

    public async Task<IReadOnlyList<JsonElement>> RunOneShotManyAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default)
    {
        // The Command Log shows what the user invoked, not the BuildArgs noise
        // (--pipe, --format jsonl, --non-interactive, --verbose); recording the
        // raw args keeps the log readable.
        var entry = _commandLog?.Begin(args, isStream: false);

        Process process;
        IReadOnlyList<string> fullArgs;
        try
        {
            fullArgs = BuildArgs(args);
            process = StartProcess(fullArgs);
        }
        catch (Exception ex)
        {
            if (entry is not null)
                _commandLog?.Fail(entry, ex);
            throw;
        }

        using (process)
        {
            var stdoutTask = ReadAllLinesAsync(process.StandardOutput, cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                if (entry is not null)
                    _commandLog?.Cancel(entry);
                throw;
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (entry is not null)
            {
                _commandLog?.Complete(
                    entry,
                    process.ExitCode,
                    stdout: string.Join('\n', stdout),
                    stderr: stderr);
            }

            if (process.ExitCode != 0)
            {
                throw new TwCliException(process.ExitCode, Format(fullArgs), stderr);
            }

            var results = new List<JsonElement>(stdout.Count);
            foreach (var line in stdout)
            {
                if (!TryParseLine(line, out var element))
                    continue;

                // `tw` list-style commands (runners list, store list, measures list, ...) go
                // through OutputContext.WriteTable → RenderJsonArray, which emits the whole
                // collection as a single JSON array on one line. Unwrap it so callers always
                // see one JsonElement per item.
                if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in element.EnumerateArray())
                        results.Add(child.Clone());
                }
                else
                {
                    results.Add(element);
                }
            }

            return results;
        }
    }

    public IAsyncEnumerable<JsonElement> StreamAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default)
    {
        // The async-iterator wrapper exists purely so we can begin a Command
        // Log entry before yielding, but still complete it from a finally
        // block — `[EnumeratorCancellation]` requires the iterator method
        // signature, so we keep the iteration in StreamCoreAsync.
        var entry = _commandLog?.Begin(args, isStream: true);
        return StreamCoreAsync(args, entry, cancellationToken);
    }

    private async IAsyncEnumerable<JsonElement> StreamCoreAsync(
        IReadOnlyList<string> args,
        CommandLogEntry? logEntry,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<string> fullArgs;
        Process startedProcess;
        try
        {
            fullArgs = BuildArgs(args);
            startedProcess = StartProcess(fullArgs);
        }
        catch (Exception ex)
        {
            if (logEntry is not null)
                _commandLog?.Fail(logEntry, ex);
            throw;
        }

        using var process = startedProcess;
        using var registration = cancellationToken.Register(() => TryKill(process));

        var stderrBuffer = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderrBuffer.AppendLine(e.Data);
        };
        process.BeginErrorReadLine();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    TryKill(process);
                    yield break;
                }

                if (line is null)
                    break;

                if (TryParseLine(line, out var element))
                    yield return element;
            }
        }
        finally
        {
            if (!process.HasExited)
                TryKill(process);

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

            if (logEntry is not null)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _commandLog?.Cancel(logEntry);
                }
                else
                {
                    // Stream commands don't capture stdout in the log (lines are
                    // consumed by the caller as they arrive); only stderr and
                    // exit code are meaningful here.
                    _commandLog?.Complete(
                        logEntry,
                        process.ExitCode,
                        stdout: null,
                        stderr: stderrBuffer.ToString().TrimEnd());
                }
            }

            if (process.ExitCode != 0 && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "tw stream exited with code {ExitCode}: {Stderr}",
                    process.ExitCode,
                    stderrBuffer.ToString().Trim());
            }
        }
    }

    internal IReadOnlyList<string> BuildArgs(IReadOnlyList<string> args)
    {
        var list = new List<string>(args.Count + 4);
        list.AddRange(args);

        if (!string.IsNullOrWhiteSpace(_settings.PipeName))
        {
            list.Add("--pipe");
            list.Add(_settings.PipeName!);
        }

        if (!string.IsNullOrWhiteSpace(_settings.Machine))
        {
            list.Add("--machine");
            list.Add(_settings.Machine!);
        }

        list.Add("--format");
        list.Add("jsonl");
        list.Add("--non-interactive");
        // Always request verbose output: commands that branch on it (tw info,
        // tw measures watch, ...) return richer JSON; the rest ignore the flag
        // because JSONL serializes full row objects regardless.
        list.Add("--verbose");
        return list;
    }

    private Process StartProcess(IReadOnlyList<string> args)
    {
        var (fileName, processArgs) = ResolveCommand(args);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in processArgs)
            psi.ArgumentList.Add(arg);

        _logger.LogDebug("Starting: {File} {Args}", psi.FileName, Format(processArgs));

        try
        {
            var process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start `{psi.FileName}`.");
            return process;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex) when (ex is not TwCliException)
        {
            throw new TwCliException(
                -1,
                $"{psi.FileName} {Format(processArgs)}",
                ex.Message);
        }
    }

    /// <summary>
    /// Decides which executable to launch and how to lay out the args. In normal
    /// mode it's just the configured <c>tw</c>; in Docker mode the launch becomes
    /// <c>docker [compose] exec &lt;container&gt; tw &lt;args...&gt;</c> so the
    /// CLI runs inside the container where the named pipe actually exists.
    /// </summary>
    internal (string FileName, IReadOnlyList<string> Args) ResolveCommand(IReadOnlyList<string> args)
    {
        var container = _settings.DockerContainer;
        if (string.IsNullOrWhiteSpace(container))
            return (_settings.TwExecutablePath, args);

        var wrapped = new List<string>(args.Count + 4);
        if (_settings.UseDockerCompose)
            wrapped.Add("compose");
        wrapped.Add("exec");
        wrapped.Add(container);
        wrapped.Add("tw");
        wrapped.AddRange(args);
        return ("docker", wrapped);
    }

    private static async Task<List<string>> ReadAllLinesAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
                break;
            lines.Add(line);
        }
        return lines;
    }

    private bool TryParseLine(string line, out JsonElement element)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            element = default;
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(line);
            element = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Skipping non-JSON line from tw: {Line}", line);
            element = default;
            return false;
        }
    }

    private static string Format(IReadOnlyList<string> args)
        => string.Join(' ', args.Select(a => a.Contains(' ', StringComparison.Ordinal) ? $"\"{a}\"" : a));

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }
}
