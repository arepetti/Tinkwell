# Actions Configuration

This document describes the syntax for defining automated actions in Tinkwell's configuration files. These files, which use a `.twa` extension, are processed by the **Executor** service to react to events happening within the system.

## Syntax Overview

An actions file consists of one or more `when` blocks, each defining a listener for a specific type of event and the actions to perform when that event occurs. The file can also contain `import` directives to include definitions from other files. Lines starting with `//` are treated as comments.

```tinkwell
// Import actions from another file
import "shared_actions.twa"

// Define a listener that only loads on a specific platform
when event system_heartbeat if "platform == 'windows'" {
    name: "Log when a specific device is online"
    subject: device_thermostat

    then {
        log {
            message: $"device {{ payload.subject }} sent a heartbeat."
        }
    }
}
```

## The `when` block

Each `when` block defines a listener. It starts by specifying the event `topic` to subscribe to, and can optionally include a condition for loading the block itself.

`when event <topic> [if "<condition>"] { ... }`

-   `<topic>`: The topic of the event to listen for. This can be an identifier or a quoted string.
-   `if "<condition>"`: An optional expression that is evaluated **only when the file is loaded**. If the condition evaluates to `false`, the entire `when` block is ignored. This is useful for creating platform-specific or environment-specific actions. The available parameters (like `platform`) are the same as in [Ensamble files](./Ensamble.md).

> **Note:** For conditional logic that needs to be executed for each event (at runtime), you should use features within the agent you are calling, or use an [expression string](#property-value-types) (`@"..."`) in an agent property. The `if` condition on a `when` block is **not** for per-event filtering.

### Attributes

Inside the `when` block, you can specify attributes to further filter the events.

| Attribute | Type   | Required | Description                                                                                             |
| :-------- | :----- | :------- | :------------------------------------------------------------------------------------------------------ |
| `name`    | String | No       | A descriptive name for the listener, which can be useful for logging and debugging.                     |
| `subject` | String | No       | A filter for the `subject` field of the event. Only events with a matching subject will be processed.   |
| `verb`    | String | No       | A filter for the `verb` field of the event. Only events with a matching verb will be processed.         |
| `object`  | String | No       | A filter for the `object` field of the event. Only events with a matching object will be processed.     |

## The `then` block

The `then` block contains a list of one or more actions to be executed when a matching event is received. The actions are executed in the order they are defined.

```tinkwell
then {
    // action 1
    // action 2
    ...
}
```

## Actions

An action is defined by specifying a registered agent's name followed by a block of properties in curly braces.

`agent_name { <properties> }`

-   `agent_name`: The name of the agent to execute (e.g., `log`, `http`).
-   `<properties>`: A set of key-value pairs that configure the agent's behavior. The available properties are specific to each agent.

### Property Value Types

Property values can be of several types:

| Type               | Example                                              | Description                                                                                                                               |
| :----------------- | :--------------------------------------------------- | :---------------------------------------------------------------------------------------------------------------------------------------- |
| **String**         | `message: "Hello, World!"`                           | A standard text string.                                                                                                                   |
| **Number**         | `timeout: 5000`                                      | An integer or floating-point number.                                                                                                      |
| **Boolean**        | `enabled: true`                                      | A `true` or `false` value.                                                                                                                |
| **Expression**     | `value: @"payload.temperature > 100"`                | A string prefixed with `@` containing an expression that will be evaluated at runtime. The result of the expression is used as the value. |
| **Template**       | `body: $"Temperature is {{ payload.temperature }}."` | A string prefixed with `$` that is interpolated at runtime. You can embed values from the event's payload using `{{ }}` syntax.        |
| **Dictionary**     | `headers: { "Content-Type": "application/json" }`    | A nested block of key-value pairs.                                                                                                        |

## Imports

You can organize your action files by splitting them into multiple files and using the `import` directive to include them. The path is relative to the current file.

`import "common_logging_actions.twa"`

## Complete Example

```tinkwell
// File: /etc/tinkwell/actions.twa

import "email_notifications.twa"

// Listen for device startup events
when event device_started {
    name: "Log device startup"
    subject: "plc.*" // Wildcard matching on subject

    then {
        log {
            message: $"Device '{{ payload.subject }}' has started up."
        }
    }
}

// Listen for high temperature alerts
when event sensor_reading {
    name: "High Temperature Alert"
    subject: factory_furnace_temperature

    then {
        // First, log the event
        log {
            message: $"High temperature detected: {{ payload.value }}°C"
        }
        // Then, send an email notification (defined in the imported file)
        send_email {
            to: "ops-team@example.com"
            subject: $"High Temperature Alert for {{ payload.subject }}"
            body: $"Temperature reached {{ payload.value }}°C. Please investigate."
        }
    }
}

// Control a device based on another event
when event system_mode_changed {
    name: "Turn off lights in maintenance mode"
    subject: building_main
    verb: updated
    object: maintenance

    then {
        http_request {
            url: "http://light-controller/api/v1/lights/off"
            method: "POST"
            body: {
                zone: "all",
                reason: $"System mode changed to {{ payload.object }} by {{ payload.source }}"
            }
        }
    }
}
```

## Supported Agents

This section lists the built-in agents available for use in `then` blocks.

### log

Writes a message to the system log. This agent is useful for debugging and monitoring event flows.

**Properties**

| Property  | Type   | Required | Description                                                                    |
| :-------- | :----- | :------- | :----------------------------------------------------------------------------- |
| `message` | String | Yes      | The log message. It can be a literal string or a [template](#property-value-types) to include event data. |

**Example**
```tinkwell
then {
    log {
        message: $"A high temperature of {{ payload.value }} was detected on {{ payload.subject }}."
    }
}
```

### pass

A no-op (no operation) agent that does nothing. It can be used as a placeholder in a `then` block when no action is required, or for testing event matching without side effects. It has no properties.

**Example**
```tinkwell
then {
    pass {}
}
```
