# Actions

Actions subscribe to the event bus and execute configurable handlers in response to matching events.
They are defined in `.tw` configuration files and loaded by the **actions runlet**.

## Syntax

```tw
action <name> [when <event-name>] {
    [source = <filter>]
    [verb = <filter>]
    [on error <policy> [retry N] [delay N] [backoff N];]

    do <handler-name> [from "<assembly>"] {
        <param> = <value>
        ...
        [on error <policy> [retry N] [delay N] [backoff N];]
    }
}
```

### Modifiers

| Modifier | Description |
|----------|-------------|
| `when <name>` | Optional. Filters events by `EventEnvelope.Name` (case-insensitive). |

### Filter properties

Body properties narrow the events that trigger the action:

| Property | Description |
|----------|-------------|
| `source` | Matches `EventEnvelope.Source` (e.g. `"signals"`, `"measures"`). |
| `verb` | Matches the event verb (e.g. `"fired"`, `"changed"`, `"created"`). |

### Handler blocks

Each `do` block specifies a handler to execute.
Multiple `do` blocks per action are allowed.

- **Runlet built-ins** — `log`, `create-event`, `http-post`, and `text-send` are registered directly by the runlet; omit `from`.
- **Default assembly** — If you omit `from` on a `do` block, the loader also scans **`Tinkwell.Actions`** (see handlers below).
  Built-in names above take precedence if the same name exists in both.
- **Custom handlers** — Use `from "<assembly>"` for any other DLL that implements `IActionHandler`.

### Error handling

The optional `on error` block configures what happens when a handler fails.
It can appear at both the `action` level (default for all handlers) and the individual `do` level (overrides the action default).

**Policies:**

| Syntax | Behavior |
|--------|----------|
| `on error resume next;` | Log warning, skip this handler, continue. **This is the implicit default.** |
| `on error stop this;` | Log error, disable this handler for future invocations. |
| `on error stop application;` | Log critical, shut down the application. |
| `on error publish "event-name" { ... }` | Publish a failure event, then continue. |

**Retry modifiers** (optional, on any policy):

| Modifier | Description |
|----------|-------------|
| `retry N` | After the first failed attempt, retry up to `N` more times (total invocations: `1 + N`) before the terminal policy runs. |
| `delay N` | Base delay in milliseconds between retries (default: 1000). |
| `backoff N` | Multiplier applied per retry (default: `1` = fixed delay; `2` = exponential: delay × backoff^attempt). |

```tw
action alert when high-temp {
    on error resume next;

    do mqtt-publish {
        topic = "alerts"
        on error stop this retry 3 delay 500 backoff 2;
    }
}
```

## Examples

### Log all events (catch-all)

```tw
action log-all-events {
    do log {
        message = (format("{Source}.{Name} {Verb}: {Object}"))
    }
}
```

### React to a specific signal

```tw
action alert-high-temp when high-temperature {
    do log {
        message = (format("Temperature alert: {Name} - {Object}"))
    }
    do create-event {
        source = actions
        verb = fired
        name = (format("alert.{Name}"))
        object = (format("Reacting to {Source}.{Name}"))
    }
}
```

### Filter by verb only

```tw
action only-fires {
    verb = fired
    do log {
        message = (format("Signal {Name} fired"))
    }
}
```

### External handler: update a store entry

```tw
action record-voltage when voltage {
    verb = changed
    do update-entry {
        bucket = history
        key = (format("voltage.{CorrelationId}"))
        value = (Object)
    }
}
```

### External handler: update a measure

```tw
action reset-pump when pump-overheat {
    do update-measure {
        name = pump-state
        value = restarting
    }
}
```

## Expression model

When an action fires, the triggering `EventEnvelope` properties become expression variables:

| Variable | Type | Description |
|----------|------|-------------|
| `Source` | string | The event source (e.g. `"signals"`, `"measures"`). |
| `Verb` | string | Lowercase verb name (e.g. `"fired"`, `"changed"`). |
| `Name` | string | The event name (signal name, measure name, etc.). |
| `Object` | string? | The event object/value, or null. |
| `CorrelationId` | string? | Correlation ID for tracing causal chains. |
| `Timestamp` | DateTime | UTC timestamp of the event. |

Additionally, all `Payload` entries are flattened into the model.
Event properties take precedence over payload keys on name conflict.

### The `format()` function

Use `format()` for runtime string interpolation with named placeholders:

```tw
message = (format("Temperature alert: {Name} = {Object}"))
```

Placeholders reference the expression variables listed above.
Unknown placeholders are left as-is.
`format()` is a general-purpose built-in function available anywhere in the expression evaluator.

### Static vs runtime values

- `$"..."` templates are resolved at **parse time** — useful for config-time values.
- `(format(...))` expressions are resolved at **runtime** against the triggering event.
- Unquoted identifiers and `"..."` strings are static and passed through as-is.

## Handler reference

### Built-in handlers

#### `log`

Logs a message to the console.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `message` | Yes | The log message. Supports expressions with `format()`. |
| `level` | No | Log level: `trace`, `debug`, `information` (default) or `info`, `warning` or `warn`, `error` or `err`, `critical` or `crit`. Unrecognized values fall back to `information`. |

#### `create-event`

Publishes a new event to the event bus.
Preserves the original `CorrelationId`.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `source` | Yes | The event source. |
| `verb` | Yes | The event verb (e.g. `fired`, `changed`, `created`). Well-known verbs are mapped to `EventVerb`; others become `Custom`. |
| `name` | Yes | The event name. |
| `object` | No | The event object/value. |

#### `http-post`

Sends an HTTP request when the action runs.
Uses a shared `HttpClient` with a 30-second timeout.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `url` | Yes | Target URL; supports expressions. |
| `body` | No | Request body; supports `format()`. Omitted = empty body. |
| `content-type` | No | Media type for the body (default: `application/json`). Ignored when `body` is omitted. |
| `method` | No | HTTP method (default: `POST`). |
| `authorization` | No | Value for the `Authorization` header (e.g. `Bearer …`). |

If the server returns a non-success status (4xx/5xx), the handler **logs a warning** with the status and body; it does **not** throw, so the configured `on error` policy does not run for HTTP errors—only for exceptions (network failures, etc.).

#### `text-send`

Writes a text payload over TCP, a serial port, or a file—the outbound counterpart to the [TextQuery runlet](../Tinkwell.Runlet.TextQuery/README.md).
The `command` transport is not supported.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `transport` | Yes | `tcp`, `serial`, or `file`. |
| `send` | Yes | Text to send; supports expressions. A line terminator is appended after this value. |
| `line-terminator` | No | `lf` (default), `cr`, `crlf`, or `none`. |
| `host` | TCP | Hostname or IP (required for `tcp`). |
| `port` | No | TCP port (default: `5025`). |
| `serial-port` | Serial | Port name, e.g. `COM3` or `/dev/ttyUSB0` (required for `serial`). |
| `baudrate` | No | Serial baud rate (default: `9600`). |
| `path` | File | Absolute file path to write (required for `file`). |

Invalid `transport` or malformed `port` / `baudrate` is logged; the handler does not throw, so `on error` may not run for those cases.
For `file`, ensure the process has permission to write the path (same cautions as sysfs/GPIO on embedded Linux as in the handler remarks).

### `Tinkwell.Actions`

Built-in assembly providing measure- and store-related handlers.

#### `create-measure`

Creates a new measure definition via the measures gRPC service.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `name` | Yes | The measure name. |
| `quantity` | No | The quantity type (e.g. `"Temperature"`). |
| `unit` | No | The unit (e.g. `"Celsius"`). |
| `value` | No | Initial numeric value. |

#### `update-measure`

Sets a measure's current value via the measures gRPC service.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `name` | Yes | The measure name. |
| `value` | Yes | The new value (numeric or string). |

#### `update-entry`

Writes a key-value entry to the state store.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `bucket` | Yes | The bucket identifier. |
| `key` | Yes | The entry key. |
| `value` | Yes | The entry value. |
| `namespace` | No | The key namespace. |
| `ttl` | No | Time-to-live in seconds. |

#### `delete-entry`

Deletes an entry from the state store.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `bucket` | Yes | The bucket identifier. |
| `key` | Yes | The entry key. |
| `namespace` | No | The key namespace. |

## Creating custom action handlers

Use `from "<assembly>"` so the loader pulls in your DLL.
The default assembly for handlers without `from` is **`Tinkwell.Actions`**; runlet built-ins (`log`, `create-event`, `http-post`, `text-send`) do not need `from`.

### Project references

A handler-only class library only needs **`Tinkwell.Actions.Abstractions`** (which transitively brings `Tinkwell.Events`, `Tinkwell.Expressions`, and `Tinkwell.Configuration.Parser`).
You do **not** need to reference the full `Tinkwell.Runlet.Actions` runlet project.

### Implementation

1. Create a class library project referencing `Tinkwell.Actions.Abstractions`.
2. Implement `IActionHandler`:

```csharp
public sealed class MyHandler : IActionHandler
{
    public string Name => "my-handler";

    public async Task ExecuteAsync(
        EventEnvelope trigger,
        IReadOnlyDictionary<string, ConfigValue> parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        var value = await ActionParameterResolver.ResolveRequiredAsync(
            "my-param", parameters, trigger, evaluator, ct);

        // ... your logic here ...
    }
}
```

`ActionParameterResolver` provides additional helpers: `ResolveOptionalAsync` for optional parameters, `ResolveAllAsync` to resolve all parameters at once, and `BuildEventModel` to build the expression variable dictionary from an `EventEnvelope`.

3. Reference the assembly in your `.tw` config:

```tw
action my-action when some-event {
    do my-handler from "MyAssembly" {
        my-param = (format("value: {Object}"))
    }
}
```

### Assembly loading

The `ActionHandlerLoader` discovers all public, non-abstract `IActionHandler` implementations in the assembly and instantiates them via `ActivatorUtilities`.
Handlers can request constructor dependencies (`IServiceDiscovery`, `ILogger<T>`, etc.).

The assembly DLL must be in the runner's base directory (`AppContext.BaseDirectory`) or resolvable through the plugin system.
The `.dll` suffix is optional in the `from` value.

> **Note:** Handler discovery uses reflection (`[RequiresUnreferencedCode]`).
> If you publish with trimming enabled, ensure handler types are preserved.
