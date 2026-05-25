# Tinkwell.Lwm2m

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This library can be used independently in any .NET 10+ application — no Tinkwell installation required.

**Transport-agnostic** — you supply registration query strings, link-format bodies, and a remote `IPEndPoint`.
There is no CoAP message type; wire encoding stays in your stack or in **Tinkwell.Coap**.

For the full Tinkwell LwM2M **server, runlet, and configuration** story, see the [LwM2M reference](https://github.com/arepetti/Tinkwell/blob/main/docs/reference/lwm2m.md).

LwM2M core types for .NET: the object/resource model, IPSO Smart Object registry, path parsing, client registration, and link-format building per [OMA-TS-LightweightM2M](https://www.openmobilealliance.org/release/LightweightM2M/).
Resource data kinds use `Tinkwell.Encoding.PayloadType`; TLV and SenML-JSON **codecs** live in the **Tinkwell.Encoding** package, not in this library.

## Requirements

- **.NET 10+** (target framework `net10.0`)

## Install

```
dotnet add package Tinkwell.Lwm2m
```

## Features

- `Lwm2mPath` for parsing and constructing LwM2M URI paths (`/objectId` … `/objectId/instanceId/resourceId`)
- Object and resource definition records (`Lwm2mObjectDefinition`, `Lwm2mResourceDefinition`, `Lwm2mOperations`)
- Built-in **curated** IPSO-style registry: OMA LwM2M object **3** plus a **non-contiguous** subset of IPSO sensor objects — **3, 3300–3306, 3308, 3310–3311, 3313–3318, 3323, 3325** (not every ID in 3300–3399, and not a full OMA dump)
- Registration types: `Lwm2mRegistration`, `RegistrationDirectory`, `RegistrationParser` (query + registration payload parsing)
- `LinkFormatBuilder` to build RFC 6690 link-format registration payloads; parsing is done by `RegistrationParser` when you call `Parse`

## Quick start

### Parse an LwM2M path

```csharp
using Tinkwell.Lwm2m;

if (Lwm2mPath.TryParse("/3303/0/5700", out var path))
{
    Console.WriteLine($"Object: {path.ObjectId}");     // 3303
    Console.WriteLine($"Instance: {path.InstanceId}"); // 0
    Console.WriteLine($"Resource: {path.ResourceId}"); // 5700
}
```

### Look up IPSO object definitions

```csharp
var tempObj = IpsoObjectRegistry.Find(3303);
Console.WriteLine(tempObj?.Name); // "Temperature"

foreach (var resource in tempObj?.Resources ?? [])
    Console.WriteLine($"  {resource.ResourceId}: {resource.Name} ({resource.Type})");
```

### Registration parsing

```csharp
using System.Net;
using Tinkwell.Lwm2m.Registration;

var clientEndpoint = new IPEndPoint(IPAddress.Loopback, 5683);
var reg = RegistrationParser.Parse(
    query: "ep=device1&lt=300",
    payload: "</3/0>,</3303/0>",
    remoteEndpoint: clientEndpoint);

Console.WriteLine($"Endpoint: {reg.Endpoint}, Lifetime: {reg.Lifetime}s");
Console.WriteLine($"Objects: {string.Join(", ", reg.Objects)}");
```

### Managing registrations (lifecycle)

`RegistrationParser.Parse` produces a `Lwm2mRegistration` (lifetime, `RegisteredAt`, `ExpiresAt`, `IsExpired`).
`RegistrationDirectory` is **thread-safe** for register, update, and deregister.
`PurgeExpired` removes stale entries; if you run it concurrently with other mutations on the same client, see the remarks on the `RegistrationDirectory` class in the API reference.

```csharp
using System.Net;
using Tinkwell.Lwm2m.Registration;

var dir = new RegistrationDirectory();
var ep = new IPEndPoint(IPAddress.Loopback, 5683);

// Register (e.g. after POST /rd): parse, then store; server assigns Location.
var pending = RegistrationParser.Parse("ep=sensor1&lt=120", "</3303/0>", ep);
var live = dir.Register(pending);
Console.WriteLine($"Location: {live.Location}, expires at {live.ExpiresAt:O}, expired? {live.IsExpired}");

// Update (e.g. registration update to same location): refresh lifetime and RegisteredAt
dir.Update(live.Location, newLifetime: 300);

// Deregister (e.g. DELETE on Location)
dir.Deregister(live.Location);

// Periodically: drop clients past lifetime without a successful update
int removed = dir.PurgeExpired();
```

### Build a registration link-format body

```csharp
using Tinkwell.Lwm2m;

var body = LinkFormatBuilder.BuildRegistrationPayload(new[] { "3/0", "3303/0" });
// "</3/0>,</3303/0>" — send as the POST /rd CoAP payload
```

## Key types

| Type | Description |
|------|-------------|
| `Lwm2mPath` | Parsed object/instance/resource path |
| `Lwm2mObjectDefinition` | Object metadata (ID, name, resources) |
| `Lwm2mResourceDefinition` | Resource metadata (ID, name, `PayloadType`, operations) |
| `Lwm2mOperations` | Read, write, execute flags on resource definitions |
| `IpsoObjectRegistry` | Curated built-in registry (object 3, IPSO sensors, etc.) |
| `Lwm2mRegistration` | Client registration record (`Tinkwell.Lwm2m.Registration`); `ExpiresAt`, `IsExpired` from lifetime + `RegisteredAt` |
| `RegistrationDirectory` | Thread-safe store; `Register`, `Update`, `Deregister`, `PurgeExpired` |
| `RegistrationParser` | Parses CoAP query + link-format registration body (string inputs) |
| `LinkFormatBuilder` | Builds comma-separated CoRE Link-Format paths for registration POST bodies (RFC 6690) |

## Dependency diagram

NuGet / project reference chain.
This project references only **Tinkwell.Encoding**; it does not reference `Tinkwell.Coap` directly.
Codec implementations (TLV, SenML-JSON) are in **Tinkwell.Encoding**.

```
Tinkwell.Lwm2m
  └── Tinkwell.Encoding  (e.g. PayloadType for resource value kinds; TLV/SenML-JSON)
        └── Tinkwell.Coap  (pulled in by Tinkwell.Encoding)
```
