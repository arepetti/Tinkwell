using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Pipes;

namespace Tinkwell.Core.Tests;

public class NamedPipeProtocolTests
{
    private sealed class TestPipeClient : PipeClient
    {
        public TestPipeClient(string pipeName, int timeoutMs = 15_000)
            : base(pipeName, NullLogger.Instance, timeoutMs) { }

        public Task<string?> SendAsync(string line, CancellationToken ct = default) =>
            SendLineAsync(line, ct);
    }

    [Fact]
    public async Task PipeServer_AndClient_ExchangeSingleLine()
    {
        var name = "tw-test-" + Guid.NewGuid().ToString("N");
        var options = new PipeServerOptions
        {
            PipeName = name,
            AllowPipeNameFallback = false,
            ConnectionTimeoutMs = 20_000
        };

        await using var server = new PipeServer(
            options,
            async (conn, ct) =>
            {
                var line = await conn.ReadLineAsync(ct);
                var response = string.IsNullOrEmpty(line)
                    ? """{"status":"error","message":"empty"}"""
                    : """{"status":"ok"}""";
                await conn.WriteLineAsync(response, ct);
            },
            NullLogger.Instance);

        await server.StartAsync();

        try
        {
            var client = new TestPipeClient(server.ResolvedPipeName);
            var resp = await client.SendAsync("hello");
            Assert.NotNull(resp);
            Assert.Contains("ok", resp, StringComparison.Ordinal);
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task PipeConnection_Id_IsStableForSession()
    {
        var name = "tw-test-" + Guid.NewGuid().ToString("N");
        var options = new PipeServerOptions { PipeName = name, AllowPipeNameFallback = false };

        Guid? seen = null;
        await using var server = new PipeServer(
            options,
            async (conn, ct) =>
            {
                seen = conn.Id;
                _ = await conn.ReadLineAsync(ct);
                await conn.WriteLineAsync("""{"status":"ok"}""", ct);
            },
            NullLogger.Instance);

        await server.StartAsync();
        try
        {
            var client = new TestPipeClient(server.ResolvedPipeName);
            await client.SendAsync("x");
            Assert.NotNull(seen);
            Assert.NotEqual(Guid.Empty, seen.Value);
        }
        finally
        {
            await server.StopAsync();
        }
    }
}
