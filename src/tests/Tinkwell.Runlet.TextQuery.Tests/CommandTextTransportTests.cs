using Tinkwell.Runlet.TextQuery.Transports;

namespace Tinkwell.Runlet.TextQuery.Tests;

public class CommandTextTransportTests
{
    [Fact]
    public async Task QueryAsync_EchoCommand_ReturnsTrimmedStdout()
    {
        var cmd = OperatingSystem.IsWindows() ? "echo tw-cmd-ok" : "printf tw-cmd-ok";
        await using var transport = new CommandTextTransport(cmd);
        await transport.ConnectAsync(CancellationToken.None);

        var result = await transport.QueryAsync(null, "\n", 15_000, CancellationToken.None);

        Assert.Equal("tw-cmd-ok", result);
    }

    [Fact]
    public async Task QueryAsync_SmallDelayCommand_CompletesAndDrainsStderr()
    {
        // Ensures both stdout and stderr read tasks are exercised (concurrent drain).
        var cmd = OperatingSystem.IsWindows()
            ? "ping 127.0.0.1 -n 1 -w 0 >nul & echo out"
            : "sleep 0.05 && echo out";
        await using var transport = new CommandTextTransport(cmd);
        await transport.ConnectAsync(CancellationToken.None);

        var result = await transport.QueryAsync(null, "\n", 15_000, CancellationToken.None);

        Assert.Equal("out", result);
    }

    [Fact]
    public async Task QueryAsync_Cancelled_ThrowsOrCancelled()
    {
        // Long-running / blocking read should observe cancellation of the wait timeout
        // (linked to the test token).
        var cmd = OperatingSystem.IsWindows()
            ? "ping 127.0.0.1 -n 30"
            : "sleep 120";
        await using var transport = new CommandTextTransport(cmd);
        await transport.ConnectAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(80);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await transport.QueryAsync(null, "\n", 60_000, cts.Token);
        });
    }
}
