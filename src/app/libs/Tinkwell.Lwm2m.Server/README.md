# Tinkwell.Lwm2m.Server

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This library can be used independently in any .NET 10+ application — no Tinkwell installation required.

A standalone LwM2M server for .NET.
Map IPSO object resources to handlers, accept client registrations, and decode TLV/SenML/text payloads automatically.
Built on [Tinkwell.Coap.Server](../Tinkwell.Coap.Server/README.md) and [Tinkwell.Lwm2m](../Tinkwell.Lwm2m/README.md).

## How it works

1. **Map resources** with `MapResource(objectId, resourceId, ...)` for each value you want to expose.
2. **Registrations (optional for data access):** clients can **POST `/rd`** to register; the server stores endpoints and link-format objects and raises `ClientRegistered` / `ClientDeregistered`.
   There is **no LwM2M bootstrap** in this package — a simple `POST /rd` is enough for most lab or integration tests.
   See [Scope](#scope-and-limitations) for the data-plane vs. registration distinction.
3. **Read and write** via CoAP to **`/objectId/instanceId/resourceId`** (for example `/3303/0/5700` for object 3303, instance 0, resource 5700).
   Instance **0** is a common convention, not a hard rule — the server matches any instance id the same way.
4. **Stop** by cancelling the `CancellationToken` passed to `RunAsync`, or by stopping the generic host for a `BackgroundService` registration.
   You do not need a separate `Dispose` for normal use.

## Requirements

- **.NET 10+** (target framework `net10.0`, same as [Tinkwell.Lwm2m](../Tinkwell.Lwm2m/README.md))

## Install

```
dotnet add package Tinkwell.Lwm2m.Server
```

## Quick start

The sample below matches the mutable-delegate style in [Using delegates](#using-delegates) (read/write the same in-memory `temperature` value):

```csharp
using Tinkwell.Encoding;
using Tinkwell.Lwm2m.Server;

var temperature = 22.5;
var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 5683 });

// IPSO Temperature (3303) sensor value (5700)
server.MapResource(3303, 5700,
    onRead: () => PayloadValue.FromFloat(temperature),
    onWrite: value => temperature = value.AsDouble());

await server.RunAsync(CancellationToken.None);
```

## Configuration

- **`Lwm2mServerOptions`**: `Port` (default `5683`), optional `Name` (for logging).
- **`Lwm2mServer` constructor**: `Lwm2mServer(Lwm2mServerOptions options, ILogger<Lwm2mServer>? logger = null)` — `options` must not be null.
  When `logger` is omitted, logging is a no-op ([`NullLogger`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.abstractions.nulllogger-1)).
- **Mapping order**: call `MapResource` for every object/resource **before** `RunAsync` or host start.
  Mapping after the server is running is not supported; mapping the same object/resource id again **replaces** the previous handler.

## Mapping resources

### Using delegates

The simplest way to map a resource is with `onRead`/`onWrite` delegates:

```csharp
double temperature = 22.5;

server.MapResource(3303, 5700,
    onRead: () => PayloadValue.FromFloat(temperature),
    onWrite: value => temperature = value.AsDouble());
```

### Using ILwm2mResourceHandler

For more control, implement `ILwm2mResourceHandler`:

```csharp
public class TemperatureHandler : ILwm2mResourceHandler
{
    private double _value = 22.5;

    // To indicate "no value," return null — the server responds with 4.04 Not Found.
    public PayloadValue? OnRead() => PayloadValue.FromFloat(_value);

    public void OnWrite(PayloadValue value)
    {
        _value = value.AsDouble();
        Console.WriteLine($"Temperature updated to {_value:F1}");
    }
}

server.MapResource(3303, 5700, new TemperatureHandler());
```

`MapResource` throws [`ArgumentNullException`](https://learn.microsoft.com/dotnet/api/system.argumentnullexception) if the handler is null, or (delegate overload) if `onRead` is null.

**Concurrency:** `ILwm2mResourceHandler` methods may be invoked from multiple CoAP requests at once.
If your handler mutates shared state, synchronize access (e.g. locks).
Registration events and handler invocations run on the thread that processes each CoAP request (avoid blocking for long work).

### Multiple resources

Map as many object/resource combinations as needed:

```csharp
server.MapResource(3303, 5700, onRead: () => PayloadValue.FromFloat(temperature))
      .MapResource(3303, 5701, onRead: () => PayloadValue.FromString("Cel"))
      .MapResource(3304, 5700, onRead: () => PayloadValue.FromFloat(humidity))
      .MapResource(3304, 5701, onRead: () => PayloadValue.FromString("%"));
```

## Registration lifecycle

The server automatically handles the LwM2M registration interface at `/rd`:

- **POST /rd** — Register a new client (returns a location path)
- **POST /rd/{location}** — Update an existing registration
- **DELETE /rd/{location}** — Deregister a client

### Registration events

```csharp
server.ClientRegistered += reg =>
    Console.WriteLine($"Client '{reg.Endpoint}' registered at {reg.Location}");

server.ClientDeregistered += reg =>
    Console.WriteLine($"Client '{reg.Endpoint}' deregistered");
```

`ClientDeregistered` is raised only when a client **explicitly deregisters** (successful `DELETE` to its registration path).
When the background purger removes **expired** registrations, that event is **not** raised.

### Inspecting registrations

```csharp
foreach (var reg in server.Registrations.All)
{
    Console.WriteLine($"{reg.Endpoint}: lifetime={reg.Lifetime}s, " +
                      $"objects=[{string.Join(", ", reg.Objects)}]");
}
```

### Automatic expiration

Expired registrations are automatically purged every 60 seconds.
The default lifetime is 86400 seconds (24 hours) per OMA-TS-LightweightM2M_Core-V1_1.

## Read/Write operations

### Automatic payload decoding

The server automatically decodes incoming payloads based on the Content-Format option:

| Content-Format | Decoder |
|---------------|---------|
| `text/plain` (0) | Parsed as number/string based on expected type |
| `application/octet-stream` (42) | Raw bytes |
| `application/vnd.oma.lwm2m+tlv` (11542) | TLV decoder |
| `application/senml+json` (110) | SenML JSON decoder |

The expected type for each resource is taken from the curated IPSO-style registry in [Tinkwell.Lwm2m](../Tinkwell.Lwm2m/README.md) (not every OMA-registered id has metadata here).

If decoding fails, the client receives **4.00 Bad Request** with a **generic** message (details are not exposed on the wire; check server logs for the exception).

### Response encoding

Responses are encoded based on the client's `Accept` option.
If multiple `Accept` options are present, only the **first** is used (additional values are ignored).

- **text/plain** (default): Human-readable string value
- **application/vnd.oma.lwm2m+tlv**: TLV-encoded resource
- **application/senml+json**: SenML JSON array

## Hosting integration

`Lwm2mServer` extends [`BackgroundService`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.backgroundservice).
**Standalone:** await `RunAsync` (same implementation as the hosted path’s `ExecuteAsync`).
**Hosted:** register the instance as an `IHostedService` / `BackgroundService` as below and let the host start it; do not also call `RunAsync` for that instance.
Prefer the host when the app already uses `Microsoft.Extensions.Hosting` (e.g. ASP.NET Core, generic host).

```csharp
// Register as a hosted service; MapResource runs before the host starts
builder.Services.AddSingleton(sp =>
{
    var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 5683 });
    server.MapResource(3303, 5700, onRead: () => PayloadValue.FromFloat(ReadTemp()));
    return server;
});
builder.Services.AddHostedService(sp => sp.GetRequiredService<Lwm2mServer>());
```

## Full example: temperature and humidity device management

```csharp
using Tinkwell.Encoding;
using Tinkwell.Lwm2m.Server;

var temperature = 22.5;
var humidity = 45.0;

var server = new Lwm2mServer(new Lwm2mServerOptions
{
    Port = 5683,
    Name = "device-mgmt"
});

// Temperature sensor (IPSO 3303)
server.MapResource(3303, 5700,
    onRead: () => PayloadValue.FromFloat(temperature),
    onWrite: v => temperature = v.AsDouble());
server.MapResource(3303, 5701,
    onRead: () => PayloadValue.FromString("Cel"));

// Humidity sensor (IPSO 3304)
server.MapResource(3304, 5700,
    onRead: () => PayloadValue.FromFloat(humidity),
    onWrite: v => humidity = v.AsDouble());
server.MapResource(3304, 5701,
    onRead: () => PayloadValue.FromString("%"));

// Registration events
server.ClientRegistered += reg =>
    Console.WriteLine($"[+] {reg.Endpoint} registered " +
                      $"(objects: {string.Join(", ", reg.Objects)})");
server.ClientDeregistered += reg =>
    Console.WriteLine($"[-] {reg.Endpoint} deregistered");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine("LwM2M server starting on port 5683...");
await server.RunAsync(cts.Token);
```

### Testing with the Tinkwell CLI

The `tw lwm2m` commands simulate a LwM2M client, making it easy to test the server from the command line.

```bash
# Register a virtual device (objects 3, 3303, 3304) with a 300 s lifetime
tw lwm2m register device1 3/0,3303/0,3304/0 --port 5683 -l 300

# Read the temperature sensor value
tw lwm2m read /3303/0/5700 --port 5683

# Write a new temperature (text/plain)
tw lwm2m write /3303/0/5700 25.0 --port 5683

# Update the registration (refresh lifetime)
tw lwm2m update rd/1 --port 5683 -l 600

# Deregister the device
tw lwm2m deregister rd/1 --port 5683
```

You can also use `tw coap send` for raw CoAP requests:

```bash
tw coap send get /3303/0/5700 --port 5683
tw coap send put /3303/0/5700 --port 5683 -d "25.0"
```

## Scope and limitations

- **Unregistered access:** The server does **not** require a client to have registered at `/rd` before serving `GET`/`PUT` (or `POST`) on a mapped path.
  Any CoAP client that can reach the port may read/write mapped resources; `/rd` is for tracking and lifecycle events, not a gate in front of object paths.
- **[`PayloadValue`](../Tinkwell.Encoding/README.md):** Return types for reads and writes use `Tinkwell.Encoding` — e.g. `FromFloat`, `FromString`, `AsDouble()`; see the Encoding README for the full `PayloadValue` model.
- **Observe (RFC 7641):** Not wired through `Lwm2mServer` for these resources, even though [Tinkwell.Coap.Server](../Tinkwell.Coap.Server/README.md) supports Observe for raw routes.
- **Transport security:** **UDP only** (no DTLS, no encryption at this layer).
  Do not use as an internet-facing LwM2M endpoint without an additional security story.
- **Handler exceptions:** If `OnRead` / `OnWrite` throw, the **underlying CoAP server** catches the exception, logs it, and the client usually gets **5.00 Internal Server Error**; avoid throwing for expected conditions.

Object and resource **definitions** (which IDs exist and their types) come from the **curated** IPSO-style registry in [Tinkwell.Lwm2m](../Tinkwell.Lwm2m/README.md) — a subset of [OMA LwM2M / IPSO](https://www.openmobilealliance.org/release/LightweightM2M/); the authoritative public object list is the [OMNA LwM2M object registry](https://technical.openmobilealliance.org/OMNA/LwM2M/LwM2MRegistry.html).

## Dependency diagram

```
Tinkwell.Lwm2m.Server
  ├── Tinkwell.Coap.Server  (CoAP transport, routing, Observe)
  │     └── Tinkwell.Coap   (protocol parsing)
  └── Tinkwell.Lwm2m        (object model, IPSO registry, registration)
        └── Tinkwell.Encoding  (TLV, SenML, PayloadValue)
```
