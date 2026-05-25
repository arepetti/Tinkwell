using Tinkwell.Integration;
using Tinkwell.Runlet.Mqtt;

namespace Tinkwell.Runlet.Mqtt.Tests;

public sealed class MqttMiddlewarePipelineTests
{
    private static MqttMessageContext CreateContext(
        string topic = "sensor/temp",
        string payload = "23.5",
        IReadOnlyList<MessageProperty>? userProperties = null) =>
        new()
        {
            Topic = topic,
            Payload = payload,
            ConnectionName = "test-conn",
            UserProperties = userProperties,
        };

    private static Func<MqttMessageContext, CancellationToken, Task> BuildPipeline(
        IReadOnlyList<IMqttMiddleware> middlewares,
        Func<MqttMessageContext, CancellationToken, Task> inner)
    {
        var pipeline = inner;
        for (int i=middlewares.Count - 1; i >= 0; --i)
        {
            var mw = middlewares[i];
            var next = pipeline;
            pipeline = (ctx, token) => mw.InvokeAsync(ctx, next, token);
        }
        return pipeline;
    }

    [Fact]
    public async Task NoMiddlewares_InnerExecutes()
    {
        bool innerCalled = false;
        var pipeline = BuildPipeline([], (_, _) => { innerCalled = true; return Task.CompletedTask; });
        await pipeline(CreateContext(), CancellationToken.None);
        Assert.True(innerCalled);
    }

    [Fact]
    public async Task SingleMiddleware_PassesThrough()
    {
        bool innerCalled = false;
        var mw = new PassThroughMiddleware();
        var pipeline = BuildPipeline([mw], (_, _) => { innerCalled = true; return Task.CompletedTask; });
        await pipeline(CreateContext(), CancellationToken.None);
        Assert.True(innerCalled);
    }

    [Fact]
    public async Task ShortCircuit_InnerNotCalled()
    {
        bool innerCalled = false;
        var mw = new BlockingMiddleware();
        var pipeline = BuildPipeline([mw], (_, _) => { innerCalled = true; return Task.CompletedTask; });
        await pipeline(CreateContext(), CancellationToken.None);
        Assert.False(innerCalled);
    }

    [Fact]
    public async Task Ordering_LowerOrderRunsFirst()
    {
        var log = new List<string>();
        var mw1 = new LoggingMiddleware("A", log) { ExecutionOrder = 10 };
        var mw2 = new LoggingMiddleware("B", log) { ExecutionOrder = -10 };

        var ordered = new List<IMqttMiddleware> { mw1, mw2 }
            .OrderBy(m => m.Order)
            .ToList();

        var pipeline = BuildPipeline(ordered, (_, _) => { log.Add("inner"); return Task.CompletedTask; });
        await pipeline(CreateContext(), CancellationToken.None);

        Assert.Equal(["B", "A", "inner"], log);
    }

    [Fact]
    public async Task ContextMutation_PropagatesDownstream()
    {
        string? observedTopic = null;
        string? observedPayload = null;

        var rewriter = new RewriteMiddleware("rewritten/topic", "transformed-payload");
        var pipeline = BuildPipeline([rewriter], (ctx, _) =>
        {
            observedTopic = ctx.Topic;
            observedPayload = ctx.Payload;
            return Task.CompletedTask;
        });

        await pipeline(CreateContext("original/topic", "raw"), CancellationToken.None);
        Assert.Equal("rewritten/topic", observedTopic);
        Assert.Equal("transformed-payload", observedPayload);
    }

    [Fact]
    public async Task Items_SharedAcrossMiddlewares()
    {
        object? observedValue = null;

        var setter = new ItemSetterMiddleware("device-id", "abc-123");
        var reader = new ItemReaderMiddleware("device-id", v => observedValue = v);

        var pipeline = BuildPipeline([setter, reader], (_, _) => Task.CompletedTask);
        await pipeline(CreateContext(), CancellationToken.None);

        Assert.Equal("abc-123", observedValue);
    }

    [Fact]
    public async Task UserProperties_AvailableInMiddleware()
    {
        IReadOnlyList<MessageProperty>? observed = null;

        var props = new List<MessageProperty>
        {
            new("device-token", "abc-secret"),
            new("tenant", "acme"),
        };

        var inspector = new UserPropertyInspectorMiddleware(up => observed = up);
        var pipeline = BuildPipeline([inspector], (_, _) => Task.CompletedTask);
        await pipeline(CreateContext(userProperties: props), CancellationToken.None);

        Assert.NotNull(observed);
        Assert.Equal(2, observed!.Count);
        Assert.Equal("device-token", observed[0].Name);
        Assert.Equal("abc-secret", observed[0].Value);
        Assert.Equal("tenant", observed[1].Name);
        Assert.Equal("acme", observed[1].Value);
    }

    [Fact]
    public async Task UserProperties_NullWhenAbsent()
    {
        IReadOnlyList<MessageProperty>? observed = null;

        var inspector = new UserPropertyInspectorMiddleware(up => observed = up);
        var pipeline = BuildPipeline([inspector], (_, _) => Task.CompletedTask);
        await pipeline(CreateContext(), CancellationToken.None);

        Assert.Null(observed);
    }

    // --- Test middleware implementations ---

    private sealed class PassThroughMiddleware : IMqttMiddleware
    {
        public Task InvokeAsync(MqttMessageContext context,
            Func<MqttMessageContext, CancellationToken, Task> next, CancellationToken ct)
            => next(context, ct);
    }

    private sealed class BlockingMiddleware : IMqttMiddleware
    {
        public Task InvokeAsync(MqttMessageContext context,
            Func<MqttMessageContext, CancellationToken, Task> next, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class LoggingMiddleware(string label, List<string> log) : IMqttMiddleware
    {
        public int ExecutionOrder { get; init; }
        int IMqttMiddleware.Order => ExecutionOrder;

        public async Task InvokeAsync(MqttMessageContext context,
            Func<MqttMessageContext, CancellationToken, Task> next, CancellationToken ct)
        {
            log.Add(label);
            await next(context, ct);
        }
    }

    private sealed class RewriteMiddleware(string newTopic, string newPayload) : IMqttMiddleware
    {
        public Task InvokeAsync(MqttMessageContext context,
            Func<MqttMessageContext, CancellationToken, Task> next, CancellationToken ct)
        {
            context.Topic = newTopic;
            context.Payload = newPayload;
            return next(context, ct);
        }
    }

    private sealed class ItemSetterMiddleware(string key, object value) : IMqttMiddleware
    {
        public Task InvokeAsync(MqttMessageContext context,
            Func<MqttMessageContext, CancellationToken, Task> next, CancellationToken ct)
        {
            context.Items[key] = value;
            return next(context, ct);
        }
    }

    private sealed class ItemReaderMiddleware(string key, Action<object?> callback) : IMqttMiddleware
    {
        public Task InvokeAsync(MqttMessageContext context,
            Func<MqttMessageContext, CancellationToken, Task> next, CancellationToken ct)
        {
            context.Items.TryGetValue(key, out var val);
            callback(val);
            return next(context, ct);
        }
    }

    private sealed class UserPropertyInspectorMiddleware(
        Action<IReadOnlyList<MessageProperty>?> callback) : IMqttMiddleware
    {
        public Task InvokeAsync(MqttMessageContext context,
            Func<MqttMessageContext, CancellationToken, Task> next, CancellationToken ct)
        {
            callback(context.UserProperties);
            return next(context, ct);
        }
    }
}
