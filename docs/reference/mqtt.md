# MQTT

Tinkwell connects to MQTT brokers via the `mqtt` runlet, subscribes to topics, and routes incoming messages through a configurable binding chain.
An embedded broker (`mqtt-server` runlet) is available for local development.

## Ensemble setup

```tw
runner mqtt-host from "Tinkwell.Runner.Headless.dll" {
    runlet mqtt from "Tinkwell.Runlet.Mqtt.dll";
}
```

The `mqtt` runlet uses `IServiceDiscovery` to find the event bus, measures service, and state store as needed by bindings.

## Configuration syntax

```tw
mqtt <connection-name> {
    broker = "<hostname>"
    port = 1883
    client-id = "tinkwell"
    # username = "user"
    # password = "secret"
    retry-count = 3
    retry-delay = 2000

    subscribe "<topic-filter>" {
        on message [when (<expression>)] {
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

Multiple `mqtt` blocks are supported for connecting to different brokers.

### Connection properties

| Property | Default | Description |
|----------|---------|-------------|
| `broker` | *(required)* | Broker hostname or IP address |
| `port` | `1883` | Broker port |
| `client-id` | `"tinkwell"` | MQTT client identifier |
| `username` | — | Optional broker username. Supports `%ENV_VAR%` expansion. |
| `password` | — | Optional broker password. Supports `%ENV_VAR%` expansion. |
| `retry-count` | `3` | Connection retry attempts |
| `retry-delay` | `2000` | Milliseconds between retries |

### Subscribe blocks

Each `subscribe` block must contain at least one `on message` block.
Topic filters use standard MQTT wildcards (`+` single level, `#` multi-level).

- **`on message`** — groups bindings for incoming messages.
  Optional `when (expression)` skips the block when the expression is falsy.
- **`bind <name> [from "<assembly>"]`** — loads a binding.
  `from` is optional for built-in bindings (they default to `Tinkwell.Integrations`).
  Optional `when (expression)` skips this binding when falsy.

### Built-in bindings

| Binding | Description |
|---------|-------------|
| `event` | Publishes an event. Parameters: `source`, `verb`, `name`, `object`; `with payload { ... }` for payload entries. |
| `measure` | Writes the message payload as a measure value. Parameter: `name`. |
| `store` | Writes to the state store. Parameters: `bucket`, `key`, optional `namespace`, `value`, `ttl`. |

### Expression context

Expressions in MQTT bindings have access to:

| Parameter | Type | Description |
|-----------|------|-------------|
| `topic` | string | Full MQTT topic (e.g. `"sensor/temperature"`) |
| `path` | string | Same as `topic` (for binding compatibility) |
| `payload` | string | Raw message payload |

Useful functions: `segment(str, index)` splits by `/` (negative indexes count from end), `json_value(str, path)` extracts from JSON, `format(template)` replaces `{Name}` placeholders.

## Error handling

The optional `on error` directive configures failure behavior.
It can appear at the `on message` level (default for all bindings) or on individual `bind` blocks (override).

| Syntax | Behavior |
|--------|----------|
| `on error resume next;` | Log warning, skip this binding, continue. **Implicit default.** |
| `on error stop this;` | Log error, disable this binding permanently. |
| `on error stop application;` | Log critical, shut down the application. |

Retry modifiers (optional, on any policy):

| Modifier | Description |
|----------|-------------|
| `retry N` | Max retry attempts before applying the policy. |
| `delay N` | Base delay in milliseconds (default: 1000). |
| `backoff N` | Multiplier per attempt (default: 1 = fixed; 2 = exponential). |

## Middleware

The MQTT runlet supports an `IMqttMiddleware` pipeline for per-device auth, message filtering, topic rewriting, and payload transformation.
The interface and context types live in `Tinkwell.Runlet.Mqtt.Abstractions`.
Register implementations in DI; they are discovered and ordered by `Order` at startup.

## Publish action

The `mqtt-publish` action handler sends MQTT messages in response to events:

```tw
action notify-broker {
    source = signals
    verb = fired

    do mqtt-publish from "Tinkwell.Actions" {
        broker = "mqtt-broker.local"
        topic = (format("alerts/{Name}"))
        payload = (format("{Object}"))
    }
}
```

## Embedded broker

For local development, the `mqtt-server` runlet provides a minimal in-process MQTT broker (MQTTnet).
No authentication, no persistence.

```tw
runner local-broker from "Tinkwell.Runner.Headless.dll" {
    runlet mqtt-server from "Tinkwell.Runlet.MqttServer.dll" {
        port = 1883
    }
    runlet mqtt from "Tinkwell.Runlet.Mqtt.dll";
}
```

Declare `mqtt-server` before `mqtt` so the broker is listening when the client connects.

## Rate limiting

`MqttConnectionWorker` buffers incoming messages through a bounded `Channel<T>` (`max-pending-messages`, default 1000) and drops oldest messages when full.
Drops are counted and surfaced via the `IngestionDropCheck` health check.

## Examples

### Events from MQTT

```tw
mqtt sensors {
    broker = "localhost"
    subscribe "sensor/+" {
        on message {
            bind event {
                source = "mqtt"
                verb = changed
                name = (segment(topic, -1))
                object = (payload)
            }
        }
    }
}
```

### Measures from MQTT

```tw
mqtt sensors {
    broker = "localhost"
    subscribe "sensor/+" {
        on message {
            bind measure {
                name = (segment(topic, -1))
            }
        }
    }
}
```

### Conditional bindings with JSON payloads

```tw
mqtt devices {
    broker = "192.168.1.100"
    subscribe "device/+/telemetry" {
        on message {
            bind event {
                source = "mqtt"
                verb = changed
                name = (segment(topic, 1))
                object = (json_value(payload, 'value'))
                with payload {
                    unit = (json_value(payload, 'unit'))
                    device = (segment(topic, 1))
                }
            }
            bind event when (json_value(payload, 'severity') == "critical") {
                source = "mqtt"
                verb = alert
                name = (segment(topic, 1))
            }
        }
    }
}
```

### Multiple brokers

```tw
mqtt warehouse {
    broker = "broker-a.local"
    subscribe "warehouse/+" {
        on message {
            bind event {
                source = "mqtt"
                verb = changed
                name = (segment(topic, -1))
                object = (payload)
            }
        }
    }
}

mqtt factory {
    broker = "broker-b.local"
    port = 8883
    username = "factory-user"
    password = "%FACTORY_MQTT_PASSWORD%"
    subscribe "factory/+/status" {
        on message {
            bind event {
                source = "mqtt"
                verb = custom
                name = (segment(topic, 1))
                object = (payload)
            }
        }
    }
}
```

### Error handling with retry

```tw
mqtt sensors {
    broker = "localhost"
    subscribe "sensor/+" {
        on message {
            on error resume next retry 2 delay 500;

            bind event {
                source = "mqtt"
                name = (segment(topic, -1))
                on error stop this;
            }
        }
    }
}
```
