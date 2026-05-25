using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Pipes;
using Tinkwell.Runner.Hosting;

namespace Tinkwell.Runner.Hosting.Tests;

public class CoordinatorPipeClientTests
{
    [Fact]
    public async Task NotifyFatalAsync_QuotesAndSanitizesMessage_ForLineProtocol()
    {
        const string id = "abcdef12";
        var pipeName = "tw-cpc-" + Guid.NewGuid().ToString("N");
        string? receivedLine = null;

        var options = new PipeServerOptions
        {
            PipeName = pipeName,
            AllowPipeNameFallback = false
        };

        await using var server = new PipeServer(
            options,
            async (conn, ct) =>
            {
                receivedLine = await conn.ReadLineAsync(ct);
                await conn.WriteLineAsync("""{"status":"ok"}""", ct);
            },
            NullLogger.Instance);

        await server.StartAsync();

        try
        {
            var client = new CoordinatorPipeClient(server.ResolvedPipeName, NullLogger<CoordinatorPipeClient>.Instance, 20_000);
            await client.NotifyFatalAsync(
                id,
                "a\nb\tc\"d\\e" + (char)5 + "z");

            Assert.NotNull(receivedLine);
            Assert.StartsWith("notify fatal " + id + " ", receivedLine, StringComparison.Ordinal);
            Assert.DoesNotContain('\n', receivedLine);
            Assert.Contains("a b c", receivedLine);
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task SendAsync_ParsesJsonResponse()
    {
        var pipeName = "tw-cpc2-" + Guid.NewGuid().ToString("N");
        var options = new PipeServerOptions
        {
            PipeName = pipeName,
            AllowPipeNameFallback = false
        };

        await using var server = new PipeServer(
            options,
            async (conn, ct) =>
            {
                _ = await conn.ReadLineAsync(ct);
                await conn.WriteLineAsync("""{"status":"ok","data":{"x":1}}""", ct);
            },
            NullLogger.Instance);

        await server.StartAsync();
        try
        {
            var client = new CoordinatorPipeClient(server.ResolvedPipeName, NullLogger<CoordinatorPipeClient>.Instance);
            var r = await client.SendAsync("notify unblock");
            Assert.True(r.IsOk);
            Assert.NotNull(r.Data);
        }
        finally
        {
            await server.StopAsync();
        }
    }
}
