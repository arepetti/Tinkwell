# How-To Guide

Practical recipes for common Tinkwell tasks.
Each section is self-contained — jump to what you need.

---

## Set up a minimal ensemble

Create a file called `ensemble.tw`:

```tw
runner main from "Tinkwell.Runner.Grpc.dll" {
    runlet store from "Tinkwell.Runlet.Store.dll" {
        storage = "memory"
    }
    runlet events from "Tinkwell.Runlet.Events.dll";
    runlet measures from "Tinkwell.Runlet.Measures.dll";
}
```

Start it:

```
tw start ensemble.tw
```

This gives you a state store, event bus, and measures registry.
Add runlets as needed — see the [runlet catalog](../architecture/runlets.md) for the full list and their required ordering.

---

## Define measures, signals, and actions

All three live in the same `.tw` file (or in separate files via `include`).
They are parsed by each runlet independently — you don't need to split them.

```tw
measure temperature {
    quantity = "Temperature"
    unit = "DegreeCelsius"
}

measure humidity {
    quantity = "RelativeHumidity"
    unit = "Percent"
}

measure heat-index {
    quantity = "Temperature"
    unit = "DegreeCelsius"
    value = ([temperature] + 0.33 * [humidity] - 4.0)
}

signal overheat when ([heat-index] > 40) until ([heat-index] < 35) for "10 seconds" {
    severity = critical
}

action notify-overheat {
    source = signals
    verb = fired

    do log {
        message = (format("ALERT: {Name} — heat index is {Object}"))
    }
}
```

Square brackets around `heat-index` are needed because the name contains a hyphen — see the [expressions reference](expressions.md#parameters).

For measures: [configuration guide — Measures](configuration.md#measures).
For signals: [configuration guide — Signals](configuration.md#signals).
For actions: [configuration guide — Actions](configuration.md#actions).

---

## Ingest data over CoAP

Add the CoAP runlet to a headless runner and define a server:

```tw
runner integrations from "Tinkwell.Runner.Headless.dll" {
    runlet coap from "Tinkwell.Runlet.Coap.dll";
}

coap server {
    port = 5683

    resource "/measures/+" {
        bind measure {
            name = (segment(path, -1))
        }
    }
}
```

A PUT to `/measures/temperature` with body `23.5` will update the `temperature` measure.

For filtering, payload extraction, and outbound bindings: [configuration guide — CoAP](configuration.md#coap-integration).

---

## Ingest data over MQTT

Add the MQTT runlet and define a connection:

```tw
runner integrations from "Tinkwell.Runner.Headless.dll" {
    runlet mqtt from "Tinkwell.Runlet.Mqtt.dll";
}

mqtt local {
    broker = "mqtt-broker.local"
    port = 1883

    subscribe "sensors/+/temperature" {
        on message {
            bind measure {
                name = (segment(topic, 1))
                value = (payload)
            }
        }
    }
}
```

A message on `sensors/floor1/temperature` with payload `22.5` updates the `floor1` measure.

For connection options, multiple subscriptions, and outbound bindings: [configuration guide — MQTT](configuration.md#mqtt-integration).

---

## Use templates to reduce repetition

```tw
template standard-runner {
    runlet store from "Tinkwell.Runlet.Store.dll" {
        storage = "memory"
    }
    runlet events from "Tinkwell.Runlet.Events.dll";
    @content
}

runner main from "Tinkwell.Runner.Grpc.dll" using standard-runner {
    runlet measures from "Tinkwell.Runlet.Measures.dll";
    runlet signals from "Tinkwell.Runlet.Signals.dll";
}
```

`@content` is replaced by whatever the consuming block declares.
For details: [configuration guide — Templates](configuration.md#templates).

---

## Use variables and conditional blocks

```tw
set environment = production
set enable_persistence = true

runner events-host from "Tinkwell.Runner.Grpc.dll" {
    runlet events from "Tinkwell.Runlet.Events.dll";

    runlet event-persistence from "Tinkwell.Runlet.EventPersistence.dll" if (enable_persistence) {
        db-path = $"events-{{environment}}.db"
    }
}
```

`$"{{var}}"` is resolved at **parse time** from `set` values.
`if (expr)` prunes entire blocks when the condition is false.
See [configuration guide — Variables](configuration.md#variables-and-interpolation) and [Conditional Blocks](configuration.md#conditional-blocks).

---

## Configure error handling and retry

`on error` is a one-line directive that can appear inside bindings, verb blocks, actions, handlers, and derived measures:

```tw
coap server {
    resource "/data/+" {
        on post {
            on error resume next retry 2 delay 500;

            bind measure {
                name = (segment(path, -1))
                on error stop this;
            }
        }
    }
}
```

The syntax is `on error <policy> [retry N] [delay N] [backoff N];` where policy is `resume next`, `stop this`, `stop application`, or `publish "event-name" { ... }` (actions only).
More specific blocks override less specific ones.

For the full reference: [configuration guide — Error Handling](configuration.md#error-handling).

---

## Bridge CoAP to MQTT (or vice versa)

```tw
coap server {
    resource "/sensors/+" {
        on post {
            bind mqtt {
                topic = (concat("replicated/", segment(path, -1)))
                broker = "mqtt-broker.local"
            }
        }
    }
}
```

The reverse (MQTT to CoAP) works the same way with `bind coap` inside an MQTT subscription.
For a complete bidirectional example: [configuration guide — Complete Example](configuration.md#complete-example).

---

## Write a custom headless runlet

A headless runlet runs background work without exposing network endpoints.
You need one public class implementing `IRunlet`.

**Project setup:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Tinkwell.Runner.Abstractions\Tinkwell.Runner.Abstractions.csproj" />
  </ItemGroup>
</Project>
```

Add `Tinkwell.Runner.Hosting` if you need `IServiceDiscovery` or `CoordinatorPipeClient`.

**The runlet class:**

```csharp
public sealed class WatchdogRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var interval = int.TryParse(settings["interval-seconds"], out var s) ? s : 30;
        services.AddSingleton(new WatchdogOptions(interval));
        services.AddHostedService<WatchdogWorker>();
    }
}
```

**The worker** (a standard `BackgroundService`):

```csharp
internal sealed class WatchdogWorker : BackgroundService
{
    private readonly WatchdogOptions _options;
    private readonly IServiceDiscovery _discovery;

    public WatchdogWorker(WatchdogOptions options, IServiceDiscovery discovery)
    {
        _options = options;
        _discovery = discovery;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Use IServiceDiscovery to find other services in the ensemble.
        // For example, discover the measures service and poll it:
        var svc = await _discovery.DiscoverAsync("measures", stoppingToken);
        if (svc is null) return;

        var client = await _discovery.CreateInstanceAsync<Measures.MeasuresClient>(svc, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // ... your logic here ...
            await Task.Delay(TimeSpan.FromSeconds(_options.Interval), stoppingToken);
        }
    }
}

internal sealed record WatchdogOptions(int Interval);
```

**Load it in the ensemble:**

```tw
runner background from "Tinkwell.Runner.Headless.dll" {
    runlet watchdog from "MyCompany.Watchdog.dll" {
        interval-seconds = 15
    }
}
```

Settings from the `runlet` block are passed to `ConfigureServices` as an `IConfiguration` instance.
The runner handles assembly loading, DI, lifecycle, and coordinator communication.

---

## Write a custom gRPC runlet

A gRPC runlet exposes a protobuf service that other runlets (or the CLI) can discover and call.

**1.
Define the proto:**

```protobuf
syntax = "proto3";
package mycompany.alerts;
option csharp_namespace = "MyCompany.Alerts.Grpc";

service AlertService {
  rpc GetActive(GetActiveRequest) returns (GetActiveResponse);
  rpc Acknowledge(AcknowledgeRequest) returns (AcknowledgeResponse);
}

message GetActiveRequest {}
message GetActiveResponse { repeated Alert alerts = 1; }
message Alert { string name = 1; string severity = 2; }
message AcknowledgeRequest { string name = 1; }
message AcknowledgeResponse {}
```

**2.
Project setup:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Google.Protobuf" />
    <PackageReference Include="Grpc.AspNetCore" />
    <PackageReference Include="Grpc.Tools" PrivateAssets="All" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Tinkwell.Runner.Abstractions\Tinkwell.Runner.Abstractions.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Protobuf Include="Protos\alerts.proto" GrpcServices="Server" />
  </ItemGroup>
</Project>
```

**3.
Implement the service** (in a `Grpc/` subfolder, per [conventions](../contributing/conventions.md#grpc-service-placement)):

```csharp
namespace MyCompany.Alerts.Grpc;

public sealed class AlertGrpcService : AlertService.AlertServiceBase
{
    private readonly AlertStore _store;

    public AlertGrpcService(AlertStore store) => _store = store;

    public override Task<GetActiveResponse> GetActive(
        GetActiveRequest request, ServerCallContext context)
    {
        var response = new GetActiveResponse();
        response.Alerts.AddRange(_store.GetActive().Select(a =>
            new Alert { Name = a.Name, Severity = a.Severity }));
        return Task.FromResult(response);
    }

    // ... Acknowledge, etc.
}
```

**4.
The runlet class** — implement `IGrpcRunlet`:

```csharp
public sealed class AlertsRunlet : IGrpcRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        services.AddSingleton<AlertStore>();
        // Register workers, config, etc.
    }

    public void MapGrpcServices(IServiceCollection services)
    {
        services.AddSingleton<AlertGrpcService>();
    }

    public void MapGrpcEndpoints(IGrpcEndpointMapper mapper)
    {
        mapper.MapService<AlertGrpcService>(opts =>
        {
            opts.FriendlyName = "Alerts";
            opts.FamilyName = "alerts";
        });
    }
}
```

`MapGrpcServices` registers the service type.
`MapGrpcEndpoints` maps it into the runner's Kestrel pipeline and registers it for discovery.
Other runlets can then find it:

```csharp
var svc = await discovery.DiscoverAsync("alerts", ct);
var client = await discovery.CreateInstanceAsync<AlertService.AlertServiceClient>(svc!, ct);
var active = await client.GetActiveAsync(new GetActiveRequest());
```

**5.
Load it:**

```tw
runner alerts-host from "Tinkwell.Runner.Grpc.dll" {
    runlet alerts from "MyCompany.Alerts.dll";
}
```

---

## Discover and call other services

Any runlet can resolve `IServiceDiscovery` from DI to find services registered by other runlets.
Always use the **family name** (e.g. `"store"`, `"measures"`) rather than a fully-qualified proto name — this lets end-users swap the default implementation for a custom one without changing consumer code:

```csharp
// Discover + create client in one step (throws if not found)
var client = await _discovery.CreateInstanceAsync<StateStore.StateStoreClient>("store", ct);

// Use it
var response = await client.GetAsync(new GetRequest { Bucket = "data", Key = "voltage" }, cancellationToken: ct);
```

When the service may not be available yet (e.g. during startup), use the two-step pattern to handle the null case:

```csharp
var svc = await _discovery.DiscoverAsync("store", ct);
if (svc is null) return; // retry later
var client = await _discovery.CreateInstanceAsync<StateStore.StateStoreClient>(svc, ct);
```

Discovery works across runners — the coordinator maintains the registry and handles lookups over named pipes.
See [architecture — Service Discovery](../architecture/coordinator-runner.md#service-discovery).

---

## Interact with the system via the CLI

```bash
# Start the coordinator
tw start ensemble.tw

# Check runner health
tw runners list
tw runners health

# Work with measures
tw measures list
tw measures get temperature
tw measures set temperature 23.5
tw measures watch

# Watch events in real time
tw events watch

# Watch signals
tw signals watch

# Store operations
tw store set data voltage 230.5
tw store get data voltage
tw store watch data

# Send a raw pipe command
tw raw "service list"

# Graceful shutdown
tw quit
```

Use `--format json` for machine-readable output.
Use `--pipe <name>` to target a specific coordinator instance.

---

## Enable HTTPS

Tinkwell supports three TLS modes for gRPC communication between runners.
Configure in `appsettings.json`:

```json
{
  "Tls": {
    "Mode": "SelfSigned"
  }
}
```

| Mode | Description |
|------|-------------|
| `None` | Plain HTTP/2 (default) |
| `SelfSigned` | Auto-generated self-signed certificate |
| `Standard` | User-provided certificate via `CertificatePath` |

For the full setup including OS trust: [Enabling HTTPS](../reference/https.md).

---

## Receive LwM2M-style data without the LwM2M runlet

If your devices report to well-known LwM2M URIs using plain text payloads and you don't need registration semantics, you can handle them with standard CoAP bindings.
This is simpler than the full LwM2M runlet and works when all you need is data ingestion:

```tw
runner integrations from "Tinkwell.Runner.Headless.dll" {
    runlet coap from "Tinkwell.Runlet.Coap.dll";
}

coap lwm2m-lite {
    port = 5683

    # Temperature sensor (IPSO object 3303, resource 5700 = Sensor Value)
    resource "/3303/+/5700" {
        on put {
            bind measure {
                name = (concat("temp-", segment(path, 2)))
            }
            bind event {
                source = coap
                verb = changed
                name = (concat("temp-", segment(path, 2)))
                object = (payload)
            }
        }
    }

    # Humidity sensor (IPSO object 3304, resource 5700)
    resource "/3304/+/5700" {
        on put {
            bind measure {
                name = (concat("humidity-", segment(path, 2)))
            }
        }
    }

    # Lightweight registration (store endpoint name with TTL)
    resource "/rd" {
        on post {
            bind store {
                bucket = "lwm2m-clients"
                key = (segment(query, 0))
                value = (payload)
                ttl = 86400
            }
        }
    }
}
```

The instance ID (second path segment) distinguishes multiple sensor instances on the same device.
The `/rd` resource provides basic client tracking via the store's TTL mechanism.

This approach does not handle TLV/SenML encoding, proper registration lifecycle, or Observe notifications.
For full LwM2M support, use the dedicated `lwm2m` runlet — see the [configuration guide](configuration.md).

---

## Split configuration across files

Use `include` at the top of a file to inline another file:

```tw
include "measures.tw"
include "signals.tw"
include "integrations/coap.tw"
include "integrations/mqtt.tw"

runner main from "Tinkwell.Runner.Grpc.dll" {
    # ...
}
```

Includes are resolved relative to the including file, recursively, and deduplicated.
See [configuration guide — Includes](configuration.md#includes).
