using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Runner;
using Tinkwell.Runlet.Actions.Handlers;
using EventsGrpc = Tinkwell.Runlet.Events.Grpc.V1;

namespace Tinkwell.Runlet.Actions.Tests;

public class CreateEventActionHandlerTests
{
    private static readonly IExpressionEvaluator Evaluator = new ExpressionEvaluator();

    private static EventEnvelope Trigger() => new()
    {
        Source = "upstream",
        Verb = EventVerb.Fired,
        Name = "seed",
        CorrelationId = "corr-99",
    };

    [Fact]
    public async Task ExecuteAsync_EventBusMissing_LogsWarningAndDoesNotThrow()
    {
        var sink = new TestLoggerSink();
        var handler = new CreateEventActionHandler(new NoEventsDiscovery(), sink.CreateLogger<CreateEventActionHandler>());

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["source"] = new StringValue("out"),
            ["verb"] = new StringValue("fired"),
            ["name"] = new StringValue("evt"),
        };

        await handler.ExecuteAsync(Trigger(), parameters, Evaluator, CancellationToken.None);

        Assert.Contains(
            sink.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("Event bus not found", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_ResolvesParametersAndPublishesMappedRequest()
    {
        var capturing = new CapturingEventBusClient();
        var discovery = new FixedEventsDiscovery(capturing);
        var sink = new TestLoggerSink();
        var handler = new CreateEventActionHandler(discovery, sink.CreateLogger<CreateEventActionHandler>());

        var parameters = new Dictionary<string, ConfigValue>
        {
            ["source"] = new StringValue("lab"),
            ["verb"] = new StringValue("changed"),
            ["name"] = new StringValue("temp"),
            ["object"] = new StringValue("21.5"),
        };

        await handler.ExecuteAsync(Trigger(), parameters, Evaluator, CancellationToken.None);

        Assert.Single(capturing.Published);
        var req = capturing.Published[0];
        Assert.Equal("lab", req.Source);
        Assert.Equal(EventsGrpc.EventVerb.Changed, req.Verb);
        Assert.Equal("temp", req.Name);
        Assert.Equal("21.5", req.Object);
        Assert.Equal("corr-99", req.CorrelationId);
    }

    private sealed class NoEventsDiscovery : IServiceDiscovery
    {
        public Task<ServiceDefinition?> DiscoverByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<ServiceDefinition?>(null);

        public Task<IReadOnlyList<ServiceDefinition>> SearchByNamePartialMatchAsync(
            string? query = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ServiceDefinition>>([]);

        public Task<T> CreateInstanceAsync<T>(ServiceDefinition service, CancellationToken cancellationToken = default)
            where T : class =>
            throw new NotSupportedException();
    }

    private sealed class FixedEventsDiscovery : IServiceDiscovery
    {
        private readonly EventsGrpc.EventBus.EventBusClient _client;

        public FixedEventsDiscovery(EventsGrpc.EventBus.EventBusClient client) => _client = client;

        public Task<ServiceDefinition?> DiscoverByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            if (name == "events")
            {
                var svc = new ServiceDefinition(
                    Name: "tinkwell.events.v1.EventBus",
                    Type: ServiceType.Grpc,
                    FriendlyName: null,
                    FamilyName: "events",
                    Aliases: [],
                    Host: "127.0.0.1:1",
                    Url: "http://127.0.0.1:1");
                return Task.FromResult<ServiceDefinition?>(svc);
            }

            return Task.FromResult<ServiceDefinition?>(null);
        }

        public Task<IReadOnlyList<ServiceDefinition>> SearchByNamePartialMatchAsync(
            string? query = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ServiceDefinition>>([]);

        public Task<T> CreateInstanceAsync<T>(ServiceDefinition service, CancellationToken cancellationToken = default)
            where T : class
        {
            if (typeof(T) == typeof(EventsGrpc.EventBus.EventBusClient))
                return Task.FromResult((T)(object)_client);

            throw new NotSupportedException();
        }
    }

    private sealed class CapturingEventBusClient : EventsGrpc.EventBus.EventBusClient
    {
        public List<EventsGrpc.PublishEventRequest> Published { get; } = [];

        public override AsyncUnaryCall<EventsGrpc.PublishEventResponse> PublishAsync(
            EventsGrpc.PublishEventRequest request,
            CallOptions options)
        {
            Published.Add(request);
            return new AsyncUnaryCall<EventsGrpc.PublishEventResponse>(
                Task.FromResult(new EventsGrpc.PublishEventResponse()),
                Task.FromResult(Metadata.Empty),
                () => Status.DefaultSuccess,
                () => Metadata.Empty,
                () => { });
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
