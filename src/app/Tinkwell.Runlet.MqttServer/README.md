# MQTT broker (server) runlet

Minimal in-process MQTT broker for **local development** only.
Clients (including the [MQTT client runlet](../Tinkwell.Runlet.Mqtt/README.md)) can connect to publish and subscribe.
No authentication, no persistence, no telemetry.

## Declaration order

**If you use both the MQTT broker and the MQTT client runlet in the same runner, you must declare the server runlet *before* the client runlet.** The broker must be listening before the client attempts to connect.

```tw
runner mqtt-host from "Tinkwell.Runner.Headless.dll" {
    runlet mqtt-server from "Tinkwell.Runlet.MqttServer.dll" { port = 1883 }
    runlet mqtt from "Tinkwell.Runlet.Mqtt.dll"
}
```

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `port` | `1883` | TCP port the broker listens on. |

## Example

```tw
runner dev from "Tinkwell.Runner.Headless.dll" {
    runlet mqtt-server from "Tinkwell.Runlet.MqttServer.dll" { port = 1883 }
    runlet mqtt from "Tinkwell.Runlet.Mqtt.dll"
}
```

Then in your `.tw` config, point the MQTT client at localhost:

```tw
mqtt local {
    broker = "localhost"
    port = 1883
    subscribe "test/+" {
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

## Limitations

- **Local development only** — not suitable for production (no TLS, no auth, no persistence).
- Single endpoint; no WebSocket or secondary ports.
- No retained message persistence across restarts.
