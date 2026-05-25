using System.Net;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Runlet.Actions.Handlers;

namespace Tinkwell.Runlet.Actions.Tests;

public class HttpPostActionHandlerTests
{
    private static readonly IExpressionEvaluator Evaluator = new ExpressionEvaluator();

    private static EventEnvelope Trigger() => new()
    {
        Source = "signals",
        Verb = EventVerb.Fired,
        Name = "x",
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(StatusCode));
        }
    }

    [Fact]
    public async Task ExecuteAsync_PostSucceeds_CompletesWithoutLogErrors()
    {
        var inner = new RecordingHandler { StatusCode = HttpStatusCode.OK };
        using var http = new HttpClient(inner, disposeHandler: false);
        var sink = new TestLoggerSink();
        var handler = new HttpPostActionHandler(sink.CreateLogger<HttpPostActionHandler>(), http);

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["url"] = new StringValue("https://example.test/hook"),
            ["body"] = new StringValue("{\"ok\":true}"),
        };

        await handler.ExecuteAsync(Trigger(), parameters, Evaluator, CancellationToken.None);

        Assert.NotNull(inner.LastRequest);
        Assert.Equal(HttpMethod.Post, inner.LastRequest.Method);
        Assert.Equal("https://example.test/hook", inner.LastRequest.RequestUri!.ToString());
        Assert.DoesNotContain(sink.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ExecuteAsync_NonSuccess_LogsWarningWithStatus()
    {
        var inner = new RecordingHandler { StatusCode = HttpStatusCode.BadGateway };
        using var http = new HttpClient(inner, disposeHandler: false);
        var sink = new TestLoggerSink();
        var handler = new HttpPostActionHandler(sink.CreateLogger<HttpPostActionHandler>(), http);

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["url"] = new StringValue("https://example.test/fail"),
        };

        await handler.ExecuteAsync(Trigger(), parameters, Evaluator, CancellationToken.None);

        Assert.Contains(sink.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("502", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_AuthorizationHeader_SentWhenProvided()
    {
        var inner = new RecordingHandler();
        using var http = new HttpClient(inner, disposeHandler: false);
        var sink = new TestLoggerSink();
        var handler = new HttpPostActionHandler(sink.CreateLogger<HttpPostActionHandler>(), http);

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["url"] = new StringValue("https://example.test/secure"),
            ["authorization"] = new StringValue("Bearer unit-test-token"),
        };

        await handler.ExecuteAsync(Trigger(), parameters, Evaluator, CancellationToken.None);

        Assert.NotNull(inner.LastRequest);
        Assert.True(inner.LastRequest.Headers.TryGetValues("Authorization", out var values));
        Assert.Equal("Bearer unit-test-token", Assert.Single(values));
    }

    [Fact]
    public async Task ExecuteAsync_MethodOverride_UsesConfiguredVerb()
    {
        var inner = new RecordingHandler();
        using var http = new HttpClient(inner, disposeHandler: false);
        var sink = new TestLoggerSink();
        var handler = new HttpPostActionHandler(sink.CreateLogger<HttpPostActionHandler>(), http);

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["url"] = new StringValue("https://example.test/api"),
            ["method"] = new StringValue("PUT"),
        };

        await handler.ExecuteAsync(Trigger(), parameters, Evaluator, CancellationToken.None);

        Assert.Equal(HttpMethod.Put, inner.LastRequest!.Method);
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_PropagatesToSend()
    {
        var inner = new DelayForeverHandler();
        using var http = new HttpClient(inner, disposeHandler: false);
        var sink = new TestLoggerSink();
        var handler = new HttpPostActionHandler(sink.CreateLogger<HttpPostActionHandler>(), http);

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["url"] = new StringValue("https://example.test/slow"),
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await handler.ExecuteAsync(Trigger(), parameters, Evaluator, cts.Token);
        });
    }

    private sealed class DelayForeverHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
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
