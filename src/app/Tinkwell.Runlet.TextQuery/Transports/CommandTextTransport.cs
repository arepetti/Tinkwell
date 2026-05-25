using System.Diagnostics;

namespace Tinkwell.Runlet.TextQuery.Transports;

internal sealed class CommandTextTransport : ITextTransport
{
    private readonly string _command;

    public CommandTextTransport(string command) => _command = command;

    public Task ConnectAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task<string> QueryAsync(string? command, string lineTerminator, int timeoutMs, CancellationToken ct)
    {
        var isWindows = OperatingSystem.IsWindows();
        var psi = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "/bin/sh",
            Arguments = isWindows ? $"/c {_command}" : $"-c \"{_command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start command: {_command}");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var errorTask = process.StandardError.ReadToEndAsync(cts.Token);
        var output = await outputTask;
        await errorTask;
        await process.WaitForExitAsync(cts.Token);

        return output.Trim();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
