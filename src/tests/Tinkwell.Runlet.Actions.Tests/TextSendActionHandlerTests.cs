using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Runlet.Actions.Handlers;

namespace Tinkwell.Runlet.Actions.Tests;

public class TextSendActionHandlerTests
{
    private static readonly IExpressionEvaluator Evaluator = new ExpressionEvaluator();

    private static EventEnvelope Trigger() => new()
    {
        Source = "signals",
        Verb = EventVerb.Fired,
        Name = "go",
    };

    [Fact]
    public async Task ExecuteAsync_File_WritesPayloadWithLineTerminator()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tw-textsend-{Guid.NewGuid():N}.txt");
        var sink = new TestLoggerSink();
        var handler = new TextSendActionHandler(sink.CreateLogger<TextSendActionHandler>());

        try
        {
            var parameters = new Dictionary<string, ConfigValue>
            {
                ["transport"] = new StringValue("file"),
                ["path"] = new StringValue(path),
                ["send"] = new StringValue("line"),
                ["line-terminator"] = new StringValue("crlf"),
            };

            await handler.ExecuteAsync(Trigger(), parameters, Evaluator, CancellationToken.None);

            var content = await File.ReadAllTextAsync(path);
            Assert.Equal("line\r\n", content);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Tcp_SendsPayloadToListener()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var accept = listener.AcceptTcpClientAsync();
        var sink = new TestLoggerSink();
        var handler = new TextSendActionHandler(sink.CreateLogger<TextSendActionHandler>());

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["transport"] = new StringValue("tcp"),
            ["host"] = new StringValue("127.0.0.1"),
            ["port"] = new StringValue(port.ToString()),
            ["send"] = new StringValue("ping"),
            ["line-terminator"] = new StringValue("lf"),
        };

        var send = handler.ExecuteAsync(Trigger(), parameters, Evaluator, CancellationToken.None);
        using var client = await accept;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        var received = await reader.ReadToEndAsync();
        await send;

        Assert.Equal("ping\n", received);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidPort_LogsErrorAndDoesNotThrow()
    {
        var sink = new TestLoggerSink();
        var handler = new TextSendActionHandler(sink.CreateLogger<TextSendActionHandler>());

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["transport"] = new StringValue("tcp"),
            ["host"] = new StringValue("127.0.0.1"),
            ["port"] = new StringValue("not-a-port"),
            ["send"] = new StringValue("x"),
        };

        await handler.ExecuteAsync(Trigger(), parameters, Evaluator, CancellationToken.None);

        Assert.Contains(sink.Entries, e =>
            e.Level == LogLevel.Error && e.Message.Contains("not-a-port", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidTransport_LogsErrorAndDoesNotThrow()
    {
        var sink = new TestLoggerSink();
        var handler = new TextSendActionHandler(sink.CreateLogger<TextSendActionHandler>());

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["transport"] = new StringValue("udp"),
            ["send"] = new StringValue("x"),
        };

        await handler.ExecuteAsync(Trigger(), parameters, Evaluator, CancellationToken.None);

        Assert.Contains(sink.Entries, e =>
            e.Level == LogLevel.Error && e.Message.Contains("unsupported transport", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("lf", "\n")]
    [InlineData("cr", "\r")]
    [InlineData("crlf", "\r\n")]
    [InlineData("none", "")]
    public async Task ExecuteAsync_LineTerminator_ResolvesToBytes(string name, string expectedSuffix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"tw-textsend-term-{Guid.NewGuid():N}.txt");
        var sink = new TestLoggerSink();
        var handler = new TextSendActionHandler(sink.CreateLogger<TextSendActionHandler>());

        try
        {
            var parameters = new Dictionary<string, ConfigValue>
            {
                ["transport"] = new StringValue("file"),
                ["path"] = new StringValue(path),
                ["send"] = new StringValue("z"),
                ["line-terminator"] = new StringValue(name),
            };

            await handler.ExecuteAsync(Trigger(), parameters, Evaluator, CancellationToken.None);

            var content = await File.ReadAllTextAsync(path);
            Assert.Equal("z" + expectedSuffix, content);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_InvalidLineTerminator_ThrowsArgumentException()
    {
        var sink = new TestLoggerSink();
        var handler = new TextSendActionHandler(sink.CreateLogger<TextSendActionHandler>());

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["transport"] = new StringValue("file"),
            ["path"] = new StringValue(Path.Combine(Path.GetTempPath(), "unused.txt")),
            ["send"] = new StringValue("a"),
            ["line-terminator"] = new StringValue("unknown-term"),
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await handler.ExecuteAsync(Trigger(), parameters, Evaluator, CancellationToken.None);
        });
    }

    private sealed class TestLoggerSink : ILoggerFactory
    {
        public List<LogEntry> Entries { get; } = [];

        public ILogger<T> CreateLogger<T>() => new TestLogger<T>(this);
        public ILogger CreateLogger(string categoryName) => new TestLogger(this);
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }

        private sealed class TestLogger<T>(TestLoggerSink sink) : TestLogger(sink), ILogger<T>;

        private class TestLogger(TestLoggerSink sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                sink.Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
            }
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}
