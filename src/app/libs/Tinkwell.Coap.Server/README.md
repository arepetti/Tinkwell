# Tinkwell.Coap.Server

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This library can be used independently in any .NET application — no Tinkwell installation required.

A standalone CoAP server for .NET with resource routing, content-format negotiation, and [RFC 7641 Observe](https://datatracker.ietf.org/doc/html/rfc7641) support.
Built on top of [Tinkwell.Coap](../Tinkwell.Coap/README.md).

## Who this is for

.NET developers who need a CoAP endpoint for IoT / LwM2M / telemetry scenarios and want a small, dependency-light server they can embed in an existing host (console app, Generic Host, background worker).
It exposes a routing API that will feel familiar to anyone who has used ASP.NET Core minimal APIs: register handlers up-front, run the server, optionally push notifications when resources change.

## Install

```
dotnet add package Tinkwell.Coap.Server
```

## First 10 minutes

```csharp
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

// Construct the server with default options (port 5683, dual-stack IPv4/IPv6).
var server = new CoapServer(CoapServerOptions.Default);

server.MapGet("/hello", (request, ct) =>
    Task.FromResult(CoapResponse.Content(
        "Hello, CoAP!"u8.ToArray(),
        CoapContentFormat.TextPlain)));

await server.RunAsync(CancellationToken.None);
```

> **Tip — running side-by-side or in tests**: only one socket can bind a given UDP port on a given interface, so if `5683` is already taken (another CoAP daemon, or a previous instance that didn't shut down) the bind fails with `SocketException`.
> Set `Port = 0` and the OS picks a free ephemeral port; once `RunAsync` is in flight, `server.BoundPort` reports the assigned value.
> This is the pattern used by every test in this repo.

`CoapServerOptions` is immutable.
To change values:

```csharp
var options = new CoapServerOptions
{
    Port = 5683,
    Name = "sensor-hub",
    MaxConcurrentRequests = 50,
    MaxPendingRequests = 200,
};
```

Invalid values (negative port, zero concurrency, etc.) throw `ArgumentOutOfRangeException` at construction time, not at server start.

## Resource routing

Register handlers for specific methods using `MapGet`, `MapPut`, `MapPost`, and `MapDelete`:

```csharp
server.MapGet("/sensors/temperature", (request, ct) =>
{
    var payload = Encoding.UTF8.GetBytes(ReadTemperature().ToString("F1"));
    return Task.FromResult(CoapResponse.Content(payload, CoapContentFormat.TextPlain));
});

server.MapPut("/config/interval", (request, ct) =>
{
    var text = Encoding.UTF8.GetString(request.Payload.Span);
    UpdateInterval(int.Parse(text));
    return Task.FromResult(CoapResponse.Changed());
});
```

Routes must be registered **before** the server starts; calling `Map*` after `RunAsync` throws `InvalidOperationException`.

### Wildcard patterns

Path patterns support single-segment (`+`) and multi-segment (`#`) wildcards, delegated to `CoapPathMatcher` in the base library:

```csharp
// Matches /sensors/temp1/value, /sensors/humidity/value, ...
server.MapGet("/sensors/+/value", handler);

// Matches /devices/a, /devices/a/b/c, ...
server.Map("/devices/#", fullHandler);
```

Routes are evaluated in registration order, so register more specific patterns before broader ones.

### Full-control handlers

For handlers that need to respond to multiple methods, implement `ICoapRequestHandler`:

```csharp
public sealed class SensorHandler : ICoapRequestHandler
{
    public Task<CoapResponse> HandleAsync(CoapRequest request, CancellationToken ct)
        => request.Method switch
        {
            CoapMethod.Get => HandleGet(request),
            CoapMethod.Put => HandlePut(request),
            _              => Task.FromResult(CoapResponse.MethodNotAllowed()),
        };
}

server.Map("/sensors/+", new SensorHandler());
```

## Content-format negotiation

Inspect the request's `ContentFormat` and `AcceptFormats` to handle different encodings:

```csharp
server.MapGet("/data", (request, ct) =>
{
    var preferred = request.AcceptFormats.Count > 0
        ? request.AcceptFormats[0]
        : CoapContentFormat.TextPlain;

    return Task.FromResult(preferred switch
    {
        CoapContentFormat.ApplicationJson =>
            CoapResponse.Content(GetJson(), CoapContentFormat.ApplicationJson),
        _ =>
            CoapResponse.Content(GetText(), CoapContentFormat.TextPlain),
    });
});
```

## Observe support (RFC 7641)

The server automatically handles Observe registration when a client sends `GET` with `Observe = 0` and the handler returns any successful response code in the `2.01 Created` &ndash; `2.05 Content` band (the same band used by transparent Block2 splitting).
In practice Observe is paired with `GET` and almost every observation registers on `2.05 Content`; the wider band exists to also accept `2.03 Valid` (conditional Observe with ETags) and the rare profile that returns `2.01 Created` from a long-poll resource.
To be stricter (only `2.05`) or to extend the policy to non-standard codes, set `CoapServerOptions.ObserveRegistrationPredicate`:

```csharp
var options = new CoapServerOptions
{
    // Strict: only register on 2.05 Content.
    ObserveRegistrationPredicate = code => code == CoapCode.Content,
};
```

To notify observers when a resource changes, call `NotifyObservers`:

```csharp
server.MapGet("/sensors/temperature", (request, ct) =>
    Task.FromResult(CoapResponse.Content(
        Encoding.UTF8.GetBytes(currentTemp.ToString("F1")),
        CoapContentFormat.TextPlain)));

// Later, when the reading changes:
currentTemp = newReading;
server.NotifyObservers("/sensors/temperature");
```

When `NotifyObservers` is called, the server re-executes the matching `GET` handler for each observer and sends a **Non-confirmable** response with a fresh message id and the appropriate Observe sequence number.
This library does not retransmit notifications — clients that need guaranteed delivery should periodically re-register the Observe relation.
Sending as NON keeps the server simple and is allowed by RFC 7641 Section 3.2.

### Observer lifecycle

- **Registration**: automatic when the client sends `GET` with `Observe = 0` and the handler returns a 2.xx code accepted by `ObserveRegistrationPredicate` (default: `2.01` &ndash; `2.05`).
- **Deregistration**: when the client sends `GET` with `Observe = 1`, or responds with `RST` to a notification.
- **Forced cleanup**: call `server.Observers.RemoveAll(endpoint)` to drop every observer from a given client (useful after an auth change).

## Hosting integration

`CoapServer` extends `BackgroundService`, so it plugs directly into the .NET Generic Host:

```csharp
builder.Services.AddSingleton(new CoapServerOptions { Port = 5683, Name = "main" });
builder.Services.AddHostedService<CoapServer>();
```

Or use `RunAsync` in a standalone application.
For tests and short-lived scenarios, `CoapServer` implements `IAsyncDisposable`:

```csharp
await using var server = new CoapServer(new CoapServerOptions { Port = 0 });
// ... register routes, start, interact, cancel ...
```

When `Port = 0` the OS picks an ephemeral port; read `server.BoundPort` after the listener has started to discover it.

## Back-pressure and dropped requests

Two knobs control how the server protects itself from overload:

- `MaxConcurrentRequests` (default 100) — size of the internal concurrency semaphore.
- `MaxPendingRequests` (default 200) — maximum queued requests waiting for a concurrency slot.
  Set to `0` to disable this cap.
  Confirmable requests that exceed the cap are rejected with `5.03 Service Unavailable`; non-confirmable ones are silently dropped.

Both `DroppedRequests` and `DroppedNotifications` are monotonic counters exposed for metrics.
Wire them into your telemetry stack however you prefer; with `System.Diagnostics.Metrics` it looks like:

```csharp
var meter = new Meter("MyApp.Coap");
meter.CreateObservableCounter("coap.dropped.requests", () => server.DroppedRequests);
meter.CreateObservableCounter("coap.dropped.notifications", () => server.DroppedNotifications);
```

## RFC 7252 §4.5 deduplication

CoAP runs over UDP and Confirmable (CON) clients retransmit the same Message ID until they see an ACK.
The server caches the bytes of the first response sent for each `(remote endpoint, Message ID)` pair, so retransmissions receive a byte-identical reply and your handler runs **exactly once** per logical request.
This protects Block1 reassembly state, Observe registration, and any side effects in your handler from being silently duplicated on a lossy link.

Knobs:

- `DedupTtl` (default `247s`, the RFC 7252 `EXCHANGE_LIFETIME`) &mdash; how long each entry is remembered.
- `MaxDedupEntries` (default `1024`) &mdash; cap on simultaneously remembered pairs.
  When the cap is reached the oldest entry by creation time is evicted.
- `MaxDedupEntries = 0` &mdash; disables deduplication entirely.
  Only set this if you know your handler is fully idempotent and you do not care about Block1/Observe duplication; rarely the right choice on a public server.

> **Memory footprint**: each entry holds the cached response bytes plus a small header.
> With Block2 splitting on (the default), an entry is at most one block plus options &mdash; rarely more than ~1.5 KB.
> The worst-case ceiling is therefore roughly `MaxDedupEntries × ResponseBlockSize`.

Non-Confirmable (NON) requests are not deduplicated &mdash; NON has no retransmission semantics.
If you need at-most-once on a NON path, use CON instead.

## Hardening incoming datagrams

CoAP messages are typically a few hundred bytes long, but the parser will accept any UDP datagram the OS hands it.
To stop a malicious or buggy peer from forcing the server to materialise multi-megabyte option lists, every server applies `CoapMessageParseLimits` to each incoming datagram:

```csharp
var options = new CoapServerOptions
{
    ParseLimits = new CoapMessageParseLimits(
        maxMessageSize: 1152,        // RFC 7252 §4.6 unfragmented IPv4 ceiling
        maxOptionCount: 16,          // typical exchanges carry < 10
        maxOptionValueLength: 256),
};
```

The default (`8 KB` / 64 options / 4 KB option values) is comfortable for most deployments.
Tighten it on internet-facing surfaces.
Datagrams that exceed any of these limits are dropped silently &mdash; CoAP does not define a reply for malformed datagrams and replying would help an attacker confirm the listener.

`CoapConstants` exposes hard ceilings (`MaxMessageSizeCeiling`, `MaxOptionCountCeiling`, `MaxOptionValueLengthCeiling`) that no caller can exceed even by passing wider values to `CoapMessageParseLimits`; they exist purely as defence in depth.

## Handling errors

Handlers can throw — the server logs the exception and responds with `5.00 Internal Server Error`.
Prefer returning a `CoapResponse.BadRequest("...")` or `CoapResponse.Forbidden("...")` directly when you want to communicate a specific problem.

Framework-level exceptions you may run into:

| Exception | When |
|-----------|------|
| `ArgumentNullException` | Passing `null` to `MapGet`/`MapPost`/`Map`/`NotifyObservers`/`ObserverRegistry` members. |
| `ArgumentOutOfRangeException` | Invalid `CoapServerOptions` values (port, concurrency limits). |
| `InvalidOperationException` | Calling `Map*` or `Use*ExceptionFilter` after the server has started, or starting the server twice. |
| `SocketException` | UDP port already in use, socket binding errors. |

## Custom exception handling

The default mapping (handler throws &rarr; `5.00 Internal Server Error`) is fine for most apps, but real services usually want to translate domain exceptions into specific CoAP codes (a missing record into `4.04 Not Found`, a validation failure into `4.00 Bad Request`, and so on) and to push faults into a structured logging or metrics sink.
Two extension points are provided:

- `ICoapRequestExceptionFilter` &mdash; runs when **a route handler throws**.
  Filters can return a custom `CoapResponse` to override the default `5.00`, or `null` to defer to the next filter.
- `ICoapDatagramExceptionFilter` &mdash; runs when **the datagram pipeline itself faults** outside any route handler (blockwise coordinator faults, transport-send faults, post-parse exceptions).
  Observer-style: filters do not produce a response (CoAP defines none for this case).

Both follow the `MapGet`/`Map(ICoapRequestHandler)` pattern: register an interface implementation for class-based filters with state or DI, or a lambda for one-liners.
Registration must occur before `RunAsync` starts.

```csharp
public sealed class DomainExceptionFilter : ICoapRequestExceptionFilter
{
    public Task<CoapResponse?> OnExceptionAsync(
        CoapRequestExceptionContext context, CancellationToken ct) =>
        Task.FromResult<CoapResponse?>(context.Exception switch
        {
            KeyNotFoundException     => CoapResponse.NotFound(),
            ArgumentException ex     => CoapResponse.BadRequest(ex.Message),
            UnauthorizedAccessException => CoapResponse.Forbidden(),
            _                        => null, // defer to the default 5.00
        });
}

server.UseRequestExceptionFilter(new DomainExceptionFilter());

// Lambda overload for the datagram scope: push every pipeline fault to a metric.
var faultsMeter = new Meter("MyApp.Coap").CreateCounter<long>("coap.pipeline.faults");
server.UseDatagramExceptionFilter((ctx, ct) =>
{
    faultsMeter.Add(1, new KeyValuePair<string, object?>("exception", ctx.Exception.GetType().Name));
    return Task.CompletedTask;
});
```

**Ordering and isolation guarantees:**

- Request filters run in registration order; the **first** filter that returns a non-null response wins and the remaining filters are not invoked.
  When every filter returns `null`, the server falls back to `5.00 Internal Server Error`.
- Datagram filters all run (observer fan-out).
- A filter that itself throws is logged at `Error` and skipped; it never crashes the server, never propagates to other filters, and never masks the original exception.
  The original exception is always logged regardless of how filters behave.
- `OperationCanceledException` triggered by shutdown bypasses both chains.

## Blockwise transfers

Block1 (large client uploads) and Block2 (large server responses) are handled **transparently** by default, matching the behaviour already provided by `Tinkwell.Coap.CoapClient`.
Handlers see the reassembled payload once; large responses are split, cached, and served block-by-block without any code in your handler.

### Block1 — uploads

When a client sends a chunked `PUT`/`POST` (Block1 option set), the server:

1. Accumulates chunks keyed by `(endpoint, method, path)` — RFC 7959 Section 2.5 allows the token to change between chunks, so the token is not part of the key.
2. Responds `2.31 Continue` to intermediate chunks.
3. On the final chunk, invokes your handler **once** with the complete payload (`CoapRequest.Payload`) and a `CoapRequest.Block1` set to `(NUM=last, M=false, SZX=...)`.
   The handler's normal response code (typically `2.04 Changed`) is sent back, with Block1 automatically echoed.

Errors the server emits without invoking the handler:

| Situation | Response |
|-----------|----------|
| Gap between chunks or no open state | `4.08 Request Entity Incomplete` |
| Accumulated size exceeds `Block1MaxPayloadBytes` | `4.13 Request Entity Too Large` with `Size1` hint |
| Duplicate `NUM` (same as last accepted) | `2.31 Continue` (idempotent re-ack) |
| No activity for `Block1UploadTimeout` | State dropped; next chunk sees `4.08` |

### Block2 — downloads

When your handler returns a payload larger than `ResponseBlockSize`, the server:

1. Caches the full payload keyed by `(endpoint, method, path, query, token)` for `Block2CacheTtl`.
   Including the token prevents two overlapping transfers from the same client colliding on a shared cache entry.
2. Sends the first block with `Block2 = (NUM=0, M=true, SZX=...)`.
3. Serves subsequent follow-up requests (client-provided `Block2 NUM > 0`) from the cache — the handler runs **once** per transfer.
   The client should reuse the same token across all blocks of a transfer (recommended by RFC 7959 and required by this server's cache keying).
   A client that asks for a smaller `Block2 SZX` on a follow-up is served a correspondingly smaller slice of the cached payload.

If the cache expires or is evicted before the client finishes fetching, a follow-up `NUM > 0` request receives `4.08 Request Entity Incomplete`; the client restarts the transfer from block 0 (RFC 7959 Section 2.4).
This guarantees that a client never splices blocks from two different handler executions — important for time-varying resources.

### Configuration

```csharp
new CoapServerOptions
{
    // Block2 splitting — set null to disable (responses go out as a single datagram).
    ResponseBlockSize     = CoapBlockSize.Bytes1024,    // default
    Block2CacheTtl        = TimeSpan.FromSeconds(60),   // default
    MaxBlock2CacheEntries = 256,                        // default; 0 disables the cap

    // Block1 reassembly — set Block1MaxPayloadBytes = 0 to disable
    // (handlers then receive raw per-chunk Block1 requests, legacy behaviour).
    Block1MaxPayloadBytes = 64 * 1024,                  // default
    Block1UploadTimeout   = TimeSpan.FromSeconds(247),  // RFC 7252 EXCHANGE_LIFETIME
    MaxBlock1Uploads      = 256,                        // default; 0 disables the cap
};
```

Both caches enforce bounded eviction.
When a new entry would exceed its cap, Block1 evicts the **least-recently-active** in-flight upload and Block2 evicts the **oldest cached response by creation time** (FIFO).
This bounds memory against a client that spreads traffic across many resource paths or stalls transfers mid-way.

### Expert mode — handler-owned blockwise

If your handler sets `CoapResponse.Block2` itself, the server respects it and does **not** engage transparent splitting.
Combined with `Block1MaxPayloadBytes = 0`, this fully restores the legacy behaviour where the handler orchestrates `2.31 Continue` / `4.08` / `4.13` manually.

### Observe + Block2

Large Observe notifications are **not** split: the server sends a single datagram that may be IP-fragmented or dropped depending on the network path.
Keep Observe payloads small, or fan out via a separate non-observable resource when large bodies are needed.

## CoapRequest cheat sheet

| Property | Type | Description |
|----------|------|-------------|
| `Method` | `CoapMethod` | Request method (`Get`, `Post`, `Put`, `Delete`). |
| `Path` | `string` | URI path (e.g. `/sensors/temp`). |
| `Query` | `string?` | URI query without leading `?`. |
| `Payload` | `ReadOnlyMemory<byte>` | Request body. |
| `ContentFormat` | `CoapContentFormat?` | Payload content-format. |
| `AcceptFormats` | `IReadOnlyList<CoapContentFormat>` | Preferred response formats. |
| `Observe` | `int?` | Observe option value (0 = register, 1 = deregister). |
| `Block1` / `Block2` | `CoapBlockOption?` | Blockwise options from the request. |
| `RemoteEndpoint` | `IPEndPoint` | Client address. |
| `Token` | `ReadOnlyMemory<byte>` | Message token. |
| `Options` | `IReadOnlyList<CoapOption>` | All CoAP options (for vendor extensions). |

## CoapResponse factories

| Method | Code | Usage |
|--------|------|-------|
| `Content(payload, format)` | 2.05 | Successful GET |
| `Created(payload?, format?)` | 2.01 | Successful POST |
| `Changed()` | 2.04 | Successful PUT |
| `Deleted()` | 2.02 | Successful DELETE |
| `NotFound()` | 4.04 | Resource not found |
| `BadRequest(message?)` | 4.00 | Invalid request |
| `Forbidden(message?)` | 4.03 | Not authorized |
| `MethodNotAllowed()` | 4.05 | Wrong method for resource |
| `Continue(block1)` | 2.31 | Block1 ack (RFC 7959) |
| `RequestEntityIncomplete(message?)` | 4.08 | Block1 out-of-order |
| `RequestEntityTooLarge()` | 4.13 | Payload too large |
| `InternalError(message?)` | 5.00 | Server error |

## Full example: sensor server with Observe

```csharp
using System.Text;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

var temperature = 22.5;
var humidity = 45.0;

await using var server = new CoapServer(new()
{
    Port = 5683,
    Name = "sensor-hub",
});

server.MapGet("/sensors/temperature", (req, ct) =>
    Task.FromResult(CoapResponse.Content(
        Encoding.UTF8.GetBytes(temperature.ToString("F1")),
        CoapContentFormat.TextPlain)));

server.MapGet("/sensors/humidity", (req, ct) =>
    Task.FromResult(CoapResponse.Content(
        Encoding.UTF8.GetBytes(humidity.ToString("F1")),
        CoapContentFormat.TextPlain)));

server.MapPut("/sensors/temperature", (req, ct) =>
{
    temperature = double.Parse(Encoding.UTF8.GetString(req.Payload.Span));
    server.NotifyObservers("/sensors/temperature");
    return Task.FromResult(CoapResponse.Changed());
});

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await server.RunAsync(cts.Token);
```

## Non-goals

- Server-side DTLS (no CoAPs).
  Use a DTLS-terminating proxy (e.g. a lightweight `openssl s_server` or a gateway) in front of the server.
- Multicast discovery (`coap://[FF02::FD]/.well-known/core`).
- Large Observe notifications (RFC 7959 Section 3.4).
  Observe responses are not split across Block2 chunks; notifications must fit in a single datagram.
- ETag-based Block2 validation (the cache is purely time-bounded).
- Observer retransmission on CON with ACK tracking.

## Dependency diagram

```
Tinkwell.Coap.Server
  └── Tinkwell.Coap  (protocol parsing, constants, blockwise)
```

## RFC references

- [RFC 7252](https://datatracker.ietf.org/doc/html/rfc7252) — The Constrained Application Protocol (CoAP).
- [RFC 7641](https://datatracker.ietf.org/doc/html/rfc7641) — Observing Resources in the Constrained Application Protocol.
- [RFC 7959](https://datatracker.ietf.org/doc/html/rfc7959) — Block-Wise Transfers in the Constrained Application Protocol.
