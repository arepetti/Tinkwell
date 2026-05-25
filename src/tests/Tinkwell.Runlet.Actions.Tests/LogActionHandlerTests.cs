using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Runlet.Actions.Handlers;

namespace Tinkwell.Runlet.Actions.Tests;

public class LogActionHandlerTests
{
    private static readonly IExpressionEvaluator Evaluator = new ExpressionEvaluator();

    private static EventEnvelope MakeEvent() => new()
    {
        Source = "signals",
        Verb = EventVerb.Fired,
        Name = "high-temp",
        Object = "92.5",
        CorrelationId = "abc-123",
    };

    [Fact]
    public async Task Execute_LogsMessage()
    {
        var sink = new TestLoggerSink();
        var logger = sink.CreateLogger<LogActionHandler>();
        var handler = new LogActionHandler(logger);

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["message"] = new StringValue("Temperature alert!"),
        };

        await handler.ExecuteAsync(MakeEvent(), parameters, Evaluator, CancellationToken.None);

        Assert.Single(sink.Entries);
        Assert.Contains("Temperature alert!", sink.Entries[0].Message);
        Assert.Equal(LogLevel.Information, sink.Entries[0].Level);
    }

    [Fact]
    public async Task Execute_WithExpression_ResolvesMessage()
    {
        var sink = new TestLoggerSink();
        var logger = sink.CreateLogger<LogActionHandler>();
        var handler = new LogActionHandler(logger);

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["message"] = new ExpressionValue("format('Alert: {Name}')"),
        };

        await handler.ExecuteAsync(MakeEvent(), parameters, Evaluator, CancellationToken.None);

        Assert.Single(sink.Entries);
        Assert.Contains("Alert: high-temp", sink.Entries[0].Message);
    }

    [Fact]
    public async Task Execute_WithLevel_UsesSpecifiedLevel()
    {
        var sink = new TestLoggerSink();
        var logger = sink.CreateLogger<LogActionHandler>();
        var handler = new LogActionHandler(logger);

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["message"] = new StringValue("Danger!"),
            ["level"] = new StringValue("warning"),
        };

        await handler.ExecuteAsync(MakeEvent(), parameters, Evaluator, CancellationToken.None);

        Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Warning, sink.Entries[0].Level);
    }

    [Fact]
    public async Task Execute_MissingMessage_Throws()
    {
        var sink = new TestLoggerSink();
        var logger = sink.CreateLogger<LogActionHandler>();
        var handler = new LogActionHandler(logger);

        var parameters = new Dictionary<string, ConfigValue>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExecuteAsync(MakeEvent(), parameters, Evaluator, CancellationToken.None));
    }

    [Fact]
    public void Name_IsLog()
    {
        var sink = new TestLoggerSink();
        var handler = new LogActionHandler(sink.CreateLogger<LogActionHandler>());
        Assert.Equal("log", handler.Name);
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
