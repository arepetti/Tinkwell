using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Studio.Services;

/// <summary>
/// Default <see cref="ICoordinatorProbe"/>: spawns <c>tw ping</c> (or
/// <c>docker [compose] exec &lt;container&gt; tw ping</c>) inline, waits up to
/// 5 seconds, and reports back. Does not touch the shared
/// <see cref="StudioSettings"/> so candidate connections can be tried without
/// disturbing the rest of the app.
/// </summary>
public sealed class CoordinatorProbe : ICoordinatorProbe
{
    /// <summary>
    /// Probe budget per attempt. Matches the heartbeat timeout in
    /// <see cref="CoordinatorHeartbeat"/> so the dialog feels responsive while
    /// still allowing for a slightly slow named pipe handshake.
    /// </summary>
    public static TimeSpan ProbeTimeout { get; } = TimeSpan.FromSeconds(5);

    private readonly StudioSettings _settings;
    private readonly ILogger<CoordinatorProbe> _logger;

    public CoordinatorProbe(StudioSettings settings, ILogger<CoordinatorProbe> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<ProbeResult> PingAsync(CoordinatorConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var (fileName, args) = BuildCommand(connection);
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
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        _logger.LogDebug("Probing coordinator: {File} {Args}", psi.FileName, string.Join(' ', args));

        Process process;
        try
        {
            process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start `{psi.FileName}`.");
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            return ProbeResult.Failed(
                connection.Transport == CoordinatorTransport.Docker
                    ? $"Could not start `docker`: {ex.Message}"
                    : $"Could not start `{fileName}`: {ex.Message}");
        }

        using (process)
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            linked.CancelAfter(ProbeTimeout);

            var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);

            try
            {
                await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                throw;
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return ProbeResult.Failed($"Timed out after {ProbeTimeout.TotalSeconds:N0}s waiting for `tw ping` to respond.");
            }

            var stderr = (await stderrTask.ConfigureAwait(false)).Trim();
            _ = await stdoutTask.ConfigureAwait(false);

            if (process.ExitCode == 0)
                return ProbeResult.Ok;

            var message = string.IsNullOrEmpty(stderr)
                ? $"`tw ping` exited with code {process.ExitCode}."
                : $"`tw ping` exited with code {process.ExitCode}: {Shorten(stderr)}";
            return ProbeResult.Failed(message);
        }
    }

    /// <summary>
    /// Builds the executable + argv that probe a specific connection. Mirrors
    /// the logic in <see cref="TwCliProcessRunner"/> but stays self-contained so
    /// candidate probes don't depend on the shared CLI singleton.
    /// </summary>
    internal (string FileName, IReadOnlyList<string> Args) BuildCommand(CoordinatorConnection connection)
    {
        var twArgs = new List<string> { "ping" };

        switch (connection.Transport)
        {
            case CoordinatorTransport.LocalCustomPipe when !string.IsNullOrWhiteSpace(connection.PipeName):
                twArgs.Add("--pipe");
                twArgs.Add(connection.PipeName!);
                break;

            case CoordinatorTransport.Remote:
                if (!string.IsNullOrWhiteSpace(connection.PipeName))
                {
                    twArgs.Add("--pipe");
                    twArgs.Add(connection.PipeName!);
                }
                if (!string.IsNullOrWhiteSpace(connection.Machine))
                {
                    twArgs.Add("--machine");
                    twArgs.Add(connection.Machine!);
                }
                break;
        }

        twArgs.Add("--format");
        twArgs.Add("jsonl");
        twArgs.Add("--non-interactive");

        if (connection.Transport != CoordinatorTransport.Docker)
            return (_settings.TwExecutablePath, twArgs);

        // Docker mode: invoke `docker [compose] exec <container> tw <args>`.
        var dockerArgs = new List<string>(twArgs.Count + 4);
        if (connection.UseDockerCompose)
            dockerArgs.Add("compose");
        dockerArgs.Add("exec");
        dockerArgs.Add(connection.DockerContainer ?? string.Empty);
        dockerArgs.Add("tw");
        dockerArgs.AddRange(twArgs);
        return ("docker", dockerArgs);
    }

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

    private static string Shorten(string message)
    {
        const int max = 200;
        var single = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return single.Length <= max ? single : single[..max] + "...";
    }
}
