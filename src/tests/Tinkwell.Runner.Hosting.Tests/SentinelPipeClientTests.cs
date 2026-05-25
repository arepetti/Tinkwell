using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Runner.Hosting;

namespace Tinkwell.Runner.Hosting.Tests;

public class SentinelPipeClientTests
{
    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        public int StopCount { get; private set; }
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => StopCount++;
    }

    [Fact]
    public async Task QuitLine_TriggersHostShutdown()
    {
        var name = "tw-sen-" + Guid.NewGuid().ToString("N");
        var life = new FakeLifetime();

        var server = Task.Run(async () =>
        {
            await using var s = new NamedPipeServerStream(
                name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await s.WaitForConnectionAsync(CancellationToken.None);
            await using var w = new StreamWriter(s, new UTF8Encoding(false), 1024, leaveOpen: true)
            {
                NewLine = "\n",
                AutoFlush = true
            };
            await w.WriteLineAsync("quit");
        });

        var sentinel = new SentinelPipeClient(name, life, NullLogger<SentinelPipeClient>.Instance);
        await sentinel.StartAsync(CancellationToken.None);
        await server;
        await Task.Delay(300);

        Assert.Equal(1, life.StopCount);
        await sentinel.StopAsync(CancellationToken.None);
        sentinel.Dispose();
    }
}
