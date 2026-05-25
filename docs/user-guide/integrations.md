# Extending Integrations

Tinkwell's CoAP and LwM2M servers can be extended in two ways:

1. **Configuration-driven** — write a custom `IIntegrationBinding` and reference it from a `.tw` `bind` block.
2. **Code-driven** — implement `ICoapBindingProvider` or `ILwm2mResourceProvider` in a runlet and register it in DI.

Configuration-driven bindings are best when behaviour is fully declarative (filter by path, transform a value, forward to a store).
Code-driven providers are better when you need full programmatic control — custom protocols, in-memory caches, request-level validation, or anything that doesn't map cleanly to a `bind` chain.

Both approaches are in-process.
They run inside the same runner as the CoAP/LwM2M server, with no IPC overhead.

---

## Custom integration bindings (configuration-driven)

A binding is a class that implements `IIntegrationBinding` (or `ICoapIntegrationBinding` for CoAP-specific features like Accept negotiation).
The `.tw` config references it by name; the CoAP/LwM2M runlet instantiates it from the declared assembly at startup.

### Defining a binding

```csharp
using Tinkwell.Integration;
using Tinkwell.Expressions;

public sealed class EchoBinding : IIntegrationBinding
{
    public string Name => "echo";

    public Task<BindingResult?> HandleAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(context.Payload ?? "");
        return Task.FromResult<BindingResult?>(
            new BindingResult(bytes, CoapContentFormat.TextPlain));
    }
}
```

### Referencing it in `.tw`

```
coap server "my-server" {
    port 5683

    resource "/echo" {
        on GET {
            bind echo from "MyBindings.dll"
        }
    }
}
```

The binding receives an `IntegrationContext` with `Path`, `Query`, `Payload`, `Method`, and optional `PayloadBytes` / `RequestContentFormat`.
It returns a `BindingResult` (content bytes + content-format) or `null` to pass through.

Properties declared inside the `bind` block are available via `BindingParameterSet`.
This lets configuration authors parameterise your binding without changing code:

```
bind echo from "MyBindings.dll" {
    prefix "hello: "
}
```

```csharp
var prefix = parameters.GetString("prefix", "");
```

---

## Code-driven CoAP routes

When you need routes that aren't expressed in `.tw` at all — for example, a runlet that exposes a health-check endpoint, a device command API, or a custom protocol — implement `ICoapBindingProvider`.

### Defining a provider

```csharp
using Tinkwell.Integration;

public sealed class HealthCheckProvider : ICoapBindingProvider
{
    public void Configure(ICoapRouteBuilder routes)
    {
        routes.MapGet("/health", new HealthCheckHandler());
    }
}

public sealed class HealthCheckHandler : ICoapResourceHandler
{
    public Task<BindingResult?> HandleAsync(
        IntegrationContext context, CancellationToken ct)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("ok");
        return Task.FromResult<BindingResult?>(
            new BindingResult(bytes, CoapContentFormat.TextPlain));
    }
}
```

### Registering in a runlet

Register the provider in DI during `ConfigureServices`:

```csharp
public sealed class MyRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        services.AddSingleton<ICoapBindingProvider, HealthCheckProvider>();
    }

    public Task StartAsync(IServiceProvider services, CancellationToken ct)
        => Task.CompletedTask;
}
```

The CoAP runlet discovers all `ICoapBindingProvider` registrations at startup and maps their routes alongside `.tw`-configured resources.
Configuration-defined routes take priority: if both define the same path, the `.tw` binding runs first.

### Route builder API

`ICoapRouteBuilder` supports:

| Method | Description |
|---|---|
| `MapGet(pattern, handler)` | Matches CoAP GET |
| `MapPut(pattern, handler)` | Matches CoAP PUT |
| `MapPost(pattern, handler)` | Matches CoAP POST |
| `MapDelete(pattern, handler)` | Matches CoAP DELETE |
| `Map(pattern, handler)` | Matches any method |

Path patterns support `+` (single segment wildcard) and `#` (multi-segment wildcard), matching the same syntax used in `.tw` resource patterns.

---

## CoAP request middleware

For cross-cutting concerns that apply to every CoAP request — logging, authentication, rate limiting — implement `ICoapRequestMiddleware`:

```csharp
using Tinkwell.Integration;

public sealed class TimingMiddleware : ICoapRequestMiddleware
{
    public int Order => -100; // runs early (outermost)

    public async Task<BindingResult?> InvokeAsync(
        IntegrationContext context,
        Func<IntegrationContext, CancellationToken, Task<BindingResult?>> next,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await next(context, ct);
        sw.Stop();
        Console.WriteLine($"{context.Method} {context.Path} → {sw.ElapsedMilliseconds}ms");
        return result;
    }
}
```

Register it in DI:

```csharp
services.AddSingleton<ICoapRequestMiddleware, TimingMiddleware>();
```

Middlewares are ordered by `Order` (lower = outermost).
They wrap **code-driven routes only** — `.tw`-configured bindings run through the existing `BindingChainExecutor` which has its own error handling, retry, and `when` filtering.
To short-circuit a request (e.g. reject unauthenticated clients), return a `BindingResult` without calling `next`.

---

## Code-driven LwM2M resources

To expose custom LwM2M objects from a runlet — for example, a virtual sensor backed by application logic — implement `ILwm2mResourceProvider`:

```csharp
using Tinkwell.Integration;

public sealed class UptimeResourceProvider : ILwm2mResourceProvider
{
    private readonly DateTime _startTime = DateTime.UtcNow;

    public IReadOnlyList<Lwm2mResourceRegistration> GetResources() =>
    [
        new Lwm2mResourceRegistration(
            ObjectId: 3,       // OMA Device object
            ResourceId: 13,    // Current Time
            OnRead: () => DateTime.UtcNow.ToString("O")),

        new Lwm2mResourceRegistration(
            ObjectId: 3,
            ResourceId: 18,    // Device Type
            OnRead: () => "Tinkwell Virtual Device"),
    ];
}
```

Register in DI:

```csharp
services.AddSingleton<ILwm2mResourceProvider, UptimeResourceProvider>();
```

The LwM2M runlet collects all providers at startup and handles format negotiation (text/plain, TLV, SenML-JSON) transparently.
Values are exchanged as strings to keep the abstractions dependency-free — the runlet handles encoding.

For writable resources, supply an `OnWrite` delegate:

```csharp
new Lwm2mResourceRegistration(
    ObjectId: 3303,
    ResourceId: 5700,
    OnRead: () => _currentValue.ToString(),
    OnWrite: value => _currentValue = double.Parse(value))
```

---

## Cross-runner communication via events

The interfaces above are in-process: the provider runs in the same runner as the CoAP/LwM2M server.
If you need to react to integration requests from a **different** runner (e.g. a logging runlet that records every CoAP write), use the event bus.

In the integration runlet, publish an event when something interesting happens:

```csharp
public sealed class AuditBinding : IIntegrationBinding
{
    private readonly IEventBus _events;

    public AuditBinding(IEventBus events) => _events = events;
    public string Name => "audit";

    public async Task<BindingResult?> HandleAsync(
        IntegrationContext context, BindingParameterSet parameters,
        IExpressionEvaluator evaluator, CancellationToken ct)
    {
        await _events.PublishAsync("coap.write", new
        {
            context.Path,
            context.Method,
            context.Payload,
            Timestamp = DateTimeOffset.UtcNow,
        }, ct);

        return null; // pass through — don't override the response
    }
}
```

In the other runner, subscribe:

```csharp
await events.SubscribeAsync("coap.write", (msg, ct) =>
{
    Console.WriteLine($"Write on {msg.Path}");
    return Task.CompletedTask;
});
```

Events are fire-and-forget (no return value).
They cross runner boundaries via gRPC.
Use this pattern when the subscriber doesn't need to influence the response — auditing, metrics aggregation, mirroring, notifications.

---

## When to use what

| Need | Approach |
|---|---|
| Declarative request→response in `.tw` | Custom `IIntegrationBinding` |
| Programmatic CoAP route (no `.tw`) | `ICoapBindingProvider` + `ICoapResourceHandler` |
| Cross-cutting concern on CoAP | `ICoapRequestMiddleware` |
| Custom LwM2M object/resource | `ILwm2mResourceProvider` |
| React from another runner (fire-and-forget) | Event bus (`IEventBus`) |
