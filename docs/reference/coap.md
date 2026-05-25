# CoAP integration

The CoAP runlet implements a lightweight [CoAP](https://tools.ietf.org/html/rfc7252) server over UDP, allowing constrained IoT devices to interact with Tinkwell services using a RESTful request/response model.
Resources are defined in `.tw` configuration files with a pluggable binding architecture.

## Architecture

Each `coap` block defines a UDP server listening on a port.
Within a server, `resource` blocks define URL path patterns.
Each resource contains `on <verb>` blocks that group bindings by HTTP-like method (GET, POST, PUT, DELETE).
Bindings are loaded from assemblies at runtime and execute sequentially within an `on` block.

```
CoAP UDP Request
  → Match resource path pattern
    → Execute matching on-verb blocks (with optional when filter)
      → Execute bindings (with optional when filter)
        → Return last non-null binding result as response
```

## Configuration syntax

The `from` clause is optional for built-in bindings (measure, event, store); they default to `Tinkwell.Integrations`.
Use `from "<assembly>"` for custom or external bindings.

### Server block

```tw
coap <name> {
    port = 5683          # UDP port (default 5683)

    resource "<pattern>" {
        on <verb> [when (<expression>)] {
            bind <name> [from "<assembly>"] [when (<expression>)] {
                # binding-specific parameters
                with <label> {
                    # nested parameters (e.g. event payload)
                }
            }
        }
    }
}
```

### Path patterns

- `/sensor/temperature` — exact match
- `/sensor/+` — `+` matches exactly one segment
- `/sensor/#` — `#` matches zero or more trailing segments

### Verbs

CoAP methods: `get`, `post`, `put`, `delete`.
Multiple `on` blocks for the same verb are allowed — all matching blocks execute in order.

### When filters

**Block-level:** `on post when (expression) { ... }` — skips the entire block if the expression is falsy.

**Binding-level:** `bind event when (expression) { ... }` — skips this specific binding if the expression is falsy.
Other bindings in the same block still execute.

Both levels compose: the `on` filter runs first, then each `bind` filter individually.

### Error handling

The optional `on error` block configures what happens when a binding fails.
It can appear at the `on` verb level (default for all bindings in that block) or at the individual `bind` level (overrides the verb default).

**Policies:**

| Syntax | Behavior |
|--------|----------|
| `on error resume next;` | Log warning, skip this binding, continue. **This is the implicit default.** |
| `on error stop this;` | Log error, disable this binding for future invocations. |
| `on error stop application;` | Log critical, shut down the application. |

**Retry modifiers** (optional, on any policy):

| Modifier | Description |
|----------|-------------|
| `retry N` | Max retry attempts before applying the policy. |
| `delay N` | Base delay in milliseconds between retries (default: 1000). |
| `backoff N` | Multiplier per attempt (default: 1 = fixed delay; 2 = exponential). |

```tw
coap sensors {
    resource "/sensor/+" {
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

### Expression context

| Variable  | Description                               |
|-----------|-------------------------------------------|
| `path`    | Request URI path (e.g. `/sensor/temp`)    |
| `query`   | Query string if present                   |
| `payload` | Request body as string (empty for GET)    |

Use `segment(path, N)` for path parsing (negative indices count from end), `json_value(payload, "$.key")` for JSON extraction.

## Built-in bindings

### `measure` — `Tinkwell.Integrations`

Reads and writes measure values via the measures gRPC service.

| Method | Behavior | Output |
|--------|----------|--------|
| GET | Reads current value | `text/plain` (default) or `application/octet-stream` (4-byte IEEE 754 float) |
| POST/PUT | Sets value from payload | None |
| DELETE | No-op | None |

**Parameters:**
- `name` (required) — measure name (expression or literal)

### `event` — `Tinkwell.Integrations`

Publishes an event to the event bus.
Never produces output.

**Parameters:**
- `source` (required) — event source identifier
- `verb` (required) — event verb (e.g. `changed`, `created`, `fired`, or `custom:xxx`)
- `name` (required) — event name
- `object` (optional) — event object value

**Nested blocks:** `with <label> { ... }` — properties become `EventEnvelope.Payload` entries.

### `store` — `Tinkwell.Integrations`

CRUD operations on the state store via gRPC.

| Method | Behavior | Output |
|--------|----------|--------|
| GET | Reads entry | `text/plain` (default) or `application/json` |
| POST | Creates entry | None |
| PUT | Creates or updates (upsert) | None |
| DELETE | Removes entry | None |

**Parameters:**
- `bucket` (required) — bucket ID
- `key` (required) — entry key (expression or literal)
- `namespace` (optional) — key namespace (default empty)
- `value` (optional) — value for POST/PUT (defaults to request payload)
- `ttl` (optional) — TTL in seconds

## Response codes

| Code | Meaning |
|------|---------|
| 2.01 Created | POST with no binding output |
| 2.02 Deleted | DELETE with no binding output |
| 2.04 Changed | PUT with no binding output |
| 2.05 Content | Binding returned output body |
| 4.00 Bad Request | `ArgumentException` from a binding |
| 4.04 Not Found | No matching resource pattern |
| 4.05 Method Not Allowed | No `on` block for this verb |
| 5.00 Internal Server Error | Unhandled exception |

## Examples

### Basic sensor ingestion

POST sensor readings, set the measure value, and publish a `changed` event:

```tw
coap sensors {
    port = 5683

    resource "/sensor/+" {
        on post {
            bind measure {
                name = (segment(path, -1))
            }
            bind event {
                source = "coap"
                verb = changed
                name = (segment(path, -1))
                with payload {
                    device = (segment(path, 1))
                    raw = (payload)
                }
            }
        }
    }
}
```

A POST to `/sensor/temperature` with body `23.5` will:
1. Set measure `temperature` to `23.5`
2. Publish event `{source: "coap", verb: changed, name: "temperature", payload: {device: "sensor", raw: "23.5"}}`
3. Respond with `2.01 Created` (no body)

### Read-back with content negotiation

Read current measure values, supporting both text and binary formats:

```tw
coap sensors {
    resource "/sensor/+" {
        on get {
            bind measure {
                name = (segment(path, -1))
            }
        }
    }
}
```

- GET `/sensor/temperature` with Accept: text/plain → `"23.5"` (UTF-8 string)
- GET `/sensor/temperature` with Accept: application/octet-stream → 4-byte IEEE 754 float

### Combined read/write resource

Single path pattern serving GET (read) and POST (write + event):

```tw
coap sensors {
    resource "/sensor/+" {
        on get {
            bind measure {
                name = (segment(path, -1))
            }
        }

        on post {
            bind measure {
                name = (segment(path, -1))
            }
            bind event {
                source = "coap"
                verb = changed
                name = (segment(path, -1))
            }
        }
    }
}
```

### Conditional routing with `when` filters

Route critical alerts to a separate event while publishing normal telemetry:

```tw
coap devices {
    resource "/device/+" {
        on post {
            bind measure {
                name = (segment(path, -1))
            }
            bind event {
                source = "coap"
                verb = changed
                name = (segment(path, -1))
            }
            bind event when (json_value(payload, "$.severity") == "critical") {
                source = "coap"
                verb = alert
                name = (segment(path, -1))
                with details {
                    severity = (json_value(payload, "$.severity"))
                    message = (json_value(payload, "$.message"))
                }
            }
        }
    }
}
```

Only the third binding (alert event) has a `when` filter — the measure update and normal event always execute.

### State store CRUD

Full CRUD operations on the state store:

```tw
coap storage {
    port = 5684

    resource "/store/+" {
        on get {
            bind store {
                bucket = "default"
                key = (segment(path, -1))
            }
        }

        on post {
            bind store {
                bucket = "default"
                key = (segment(path, -1))
                ttl = 3600
            }
        }

        on put {
            bind store {
                bucket = "default"
                key = (segment(path, -1))
            }
        }

        on delete {
            bind store {
                bucket = "default"
                key = (segment(path, -1))
            }
        }
    }
}
```

### Block-level `when` filter

Skip an entire verb block based on a condition:

```tw
coap restricted {
    resource "/admin/+" {
        on post when (query == "auth=secret") {
            bind store {
                bucket = "admin"
                key = (segment(path, -1))
            }
        }
    }
}
```

POST requests without `?auth=secret` in the query will receive `4.05 Method Not Allowed`.

### Multiple servers

Run two CoAP servers on different ports:

```tw
coap sensors {
    port = 5683
    resource "/sensor/+" {
        on post {
            bind measure {
                name = (segment(path, -1))
            }
        }
    }
}

coap admin {
    port = 5684
    resource "/store/+" {
        on get {
            bind store {
                bucket = "admin"
                key = (segment(path, -1))
            }
        }
    }
}
```

## Ensemble configuration

```tw
runner coap-host from "Tinkwell.Runner.Headless.dll" {
    runlet coap from "Tinkwell.Runlet.Coap.dll";
}
```

The CoAP runlet discovers required services (measures, events, store) via `IServiceDiscovery`.
Ensure those runners are started before the CoAP runner.
