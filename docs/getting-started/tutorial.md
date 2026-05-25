# Tutorial

This walkthrough takes you from a blank directory to a working Tinkwell system with measures, signals, actions, and MQTT ingestion.
Each step builds on the previous one.

**Prerequisites:** Tinkwell installed and `tw` on your PATH.
See [Installation](installation.md) if you haven't set up yet.

## 1. Create a project directory

```bash
mkdir my-project
cd my-project
```

## 2. Define the ensemble

Create a file called `ensemble.tw`.
This declares which processes (runners) to start and which components (runlets) each one hosts:

```tw
runner grpc-store from "Tinkwell.Runner.Grpc.dll" {
    runlet store from "Tinkwell.Runlet.Store.dll" {
        storage = "memory"
    }
}

runner grpc-events from "Tinkwell.Runner.Grpc.dll" {
    runlet events from "Tinkwell.Runlet.Events.dll";
}

runner grpc-measures from "Tinkwell.Runner.Grpc.dll" {
    runlet measures from "Tinkwell.Runlet.Measures.dll";
    runlet signals from "Tinkwell.Runlet.Signals.dll";
}

runner actions-host from "Tinkwell.Runner.Headless.dll" {
    runlet actions from "Tinkwell.Runlet.Actions.dll";
}
```

This gives you a state store, event bus, measures registry, signal evaluation, and an action system.

## 3. Add a measure

Append to the same file (or use `include "measures.tw"` to keep things separate).
The measure is named `room` so it lines up with the MQTT example in step 8 (topic `sensors/room/temperature` maps the second segment to the measure name):

```tw
measure room {
    quantity = Temperature
    unit = DegreeCelsius
}
```

## 4. Add a signal

Signals watch measures and fire when a condition holds for a specified duration:

```tw
signal overheating when (room > 30) for "10 seconds" {
    severity = warning
}
```

## 5. Add an action

Actions react to events.
Here we log a message whenever a signal fires:

```tw
action log-alerts {
    source = signals
    verb = fired

    do log {
        message = (format("[{severity}] Signal {Name} fired"))
    }
}
```

## 6. Start the coordinator

```bash
tw start
```

Without arguments, `tw start` looks for `ensemble.tw` in the current directory.
You should see log output as each runner starts and reports ready.

## 7. Interact with the CLI

Open a second terminal in the same directory:

```bash
# List defined measures
tw measures list

# Manually set a value to test the pipeline
tw measures set room 25.0

# Watch measures in real time
tw measures watch

# Push it above the threshold
tw measures set room 32.0

# After 10 seconds, watch for the signal
tw signals watch

# Watch events (signals publish events when they fire)
tw events watch
```

## 8. Add MQTT ingestion

To feed data from an MQTT broker instead of manual CLI commands, add an MQTT runner and connection.
Append to your config:

```tw
runner mqtt-host from "Tinkwell.Runner.Headless.dll" {
    runlet mqtt from "Tinkwell.Runlet.Mqtt.dll";
}

mqtt local {
    broker = "localhost"
    port = 1883

    subscribe "sensors/+/temperature" {
        on message {
            bind measure {
                name = (segment(topic, 1))
            }
        }
    }
}
```

Now a message on topic `sensors/room/temperature` with payload `32.5` will update the `room` measure — and if a measure named `room` is defined, the full pipeline (signals, actions) runs automatically.

For local development without an external broker, add the embedded broker:

```tw
runner mqtt-host from "Tinkwell.Runner.Headless.dll" {
    runlet mqtt-server from "Tinkwell.Runlet.MqttServer.dll" {
        port = 1883
    }
    runlet mqtt from "Tinkwell.Runlet.Mqtt.dll";
}
```

Restart the coordinator to pick up changes:

```bash
tw quit
tw start
```

## Next steps

- [Configuration reference](../user-guide/configuration.md) — Full `.tw` language syntax
- [How-to recipes](../user-guide/how-to.md) — Patterns for common tasks (custom runlets, templates, error handling)
- [CLI reference](../user-guide/cli.md) — Every `tw` command and option
- [Sample use-cases](https://github.com/arepetti/Tinkwell/tree/main/samples/use-cases/) — Complete working configurations for real scenarios
