# Tinkwell Configuration Guide

This guide covers everything you need to write a Tinkwell ensemble configuration file (`.tw`).
It assumes no prior knowledge of Tinkwell internals — only that you want to configure the system to collect measurements, evaluate conditions, react to events, and communicate with devices over MQTT and CoAP.

---

## Table of Contents

- [Quick Start](#quick-start)
- [Configuration File Syntax](#configuration-file-syntax)
  - [Blocks](#blocks)
  - [Properties](#properties)
  - [Value Types](#value-types)
  - [Modifiers](#modifiers)
  - [Comments](#comments)
  - [Includes](#includes)
  - [Variables and Interpolation](#variables-and-interpolation)
  - [Templates](#templates)
  - [Conditional Blocks](#conditional-blocks)
- [Ensemble and Runners](#ensemble-and-runners)
  - [What Are Runners and Runlets?](#what-are-runners-and-runlets)
  - [Why Split Into Multiple Runners?](#why-split-into-multiple-runners)
  - [Runner Types](#runner-types)
  - [Runlet Catalog](#runlet-catalog)
  - [Declaration Order](#declaration-order)
  - [Simplified Single-Runner Layout](#simplified-single-runner-layout)
- [Measures](#measures)
  - [Defining a Measure](#defining-a-measure)
  - [Measure Properties](#measure-properties)
  - [Plain Measures](#plain-measures)
  - [Constant Measures](#constant-measures)
  - [Derived Measures](#derived-measures)
  - [String Measures](#string-measures)
  - [Units of Measure](#units-of-measure)
  - [Quantity and Unit Naming](#quantity-and-unit-naming)
  - [Runlet Dependencies (Measures)](#runlet-dependencies-measures)
- [Signals](#signals)
  - [Defining a Signal](#defining-a-signal)
  - [Top-Level Signals](#top-level-signals)
  - [Inline Signals (Inside a Measure)](#inline-signals-inside-a-measure)
  - [The `when` Clause](#the-when-clause)
  - [The `until` Clause (Hysteresis)](#the-until-clause-hysteresis)
  - [The `for` Clause (Debounce / Hold Time)](#the-for-clause-debounce--hold-time)
  - [Signal Properties](#signal-properties)
  - [Signal State Machine](#signal-state-machine)
  - [Runlet Dependencies (Signals)](#runlet-dependencies-signals)
- [Events](#events)
  - [The Event Model](#the-event-model)
  - [Well-Known Verbs](#well-known-verbs)
  - [Measure-Events Bridge](#measure-events-bridge)
  - [Event Persistence](#event-persistence)
  - [Runlet Dependencies (Events)](#runlet-dependencies-events)
- [Actions](#actions)
  - [Defining an Action](#defining-an-action)
  - [Event Filters](#event-filters)
  - [Handler Blocks (`do`)](#handler-blocks-do)
  - [Built-In Handlers](#built-in-handlers)
  - [External Action Handlers (`Tinkwell.Actions`)](#external-action-handlers-tinkwellactions)
  - [Expression Variables in Actions](#expression-variables-in-actions)
  - [The `format()` Function](#the-format-function)
  - [Runlet Dependencies (Actions)](#runlet-dependencies-actions)
- [CoAP Integration](#coap-integration)
  - [CoAP Overview](#coap-overview)
  - [Defining a CoAP Server](#defining-a-coap-server)
  - [Path Patterns](#path-patterns)
  - [Verb Blocks (`on`)](#verb-blocks-on)
  - [Binding Blocks (`bind`)](#binding-blocks-bind)
  - [When Filters](#when-filters)
  - [Built-In Bindings (CoAP)](#built-in-bindings-coap)
  - [Outbound Bindings (CoAP)](#outbound-bindings-coap)
  - [Expression Variables in CoAP](#expression-variables-in-coap)
  - [Response Codes](#response-codes)
  - [CoAP Examples](#coap-examples)
  - [Runlet Dependencies (CoAP)](#runlet-dependencies-coap)
- [MQTT Integration](#mqtt-integration)
  - [MQTT Overview](#mqtt-overview)
  - [Defining an MQTT Connection](#defining-an-mqtt-connection)
  - [Connection Properties](#connection-properties)
  - [Subscribe Blocks](#subscribe-blocks)
  - [Verb Blocks (`on message`)](#verb-blocks-on-message)
  - [Binding Blocks (MQTT)](#binding-blocks-mqtt)
  - [Built-In Bindings (MQTT)](#built-in-bindings-mqtt)
  - [Outbound Bindings (MQTT)](#outbound-bindings-mqtt)
  - [Expression Variables in MQTT](#expression-variables-in-mqtt)
  - [MQTT Examples](#mqtt-examples)
  - [Runlet Dependencies (MQTT)](#runlet-dependencies-mqtt)
- [Protobuf Gateway](#protobuf-gateway)
  - [Overview](#overview)
  - [Runlet Declaration](#runlet-declaration)
  - [Access Profiles](#access-profiles)
  - [Path Convention](#path-convention)
  - [Multiple Profiles on One Server](#multiple-profiles-on-one-server)
  - [Error Responses](#error-responses)
  - [Future Extensions](#future-extensions)
  - [Dependencies](#dependencies-6)
- [LwM2M Integration](#lwm2m-integration)
  - [LwM2M Overview](#lwm2m-overview)
  - [Defining an LwM2M Server](#defining-an-lwm2m-server)
  - [Registration](#registration)
  - [Object Mappings](#object-mappings)
  - [Read and Write Operations](#read-and-write-operations)
  - [Supported IPSO Objects](#supported-ipso-objects)
  - [Lightweight Alternative](#lightweight-alternative-no-runlet)
- [Error Handling](#error-handling)
  - [Overview](#error-handling-overview)
  - [The `on error` Block](#the-on-error-block)
  - [Error Policies](#error-policies)
  - [Retry Logic](#retry-logic)
  - [The `publish` Policy](#the-publish-policy)
  - [Policy Inheritance](#policy-inheritance)
  - [Error Handling in Actions](#error-handling-in-actions)
  - [Error Handling in CoAP Bindings](#error-handling-in-coap-bindings)
  - [Error Handling in MQTT Bindings](#error-handling-in-mqtt-bindings)
  - [Error Handling in Derived Measures](#error-handling-in-derived-measures)
- [Expressions](#expressions)
  - [Where Expressions Are Used](#where-expressions-are-used)
  - [Useful Functions](#useful-functions)
  - [The `quantity()` Function](#the-quantity-function)
- [Complete Example](#complete-example)

---

## Quick Start

A Tinkwell configuration file (conventionally named `ensamble.tw`) describes:

1. **Runners and runlets** — the processes and services that make up your system.
2. **Measures** — named numeric or string values your system tracks.
3. **Signals** — conditions evaluated against measures that fire events.
4. **Actions** — handlers that run in response to events.
5. **Integrations** — CoAP servers and MQTT connections that bridge external protocols into Tinkwell.

Here is a minimal but complete example:

```tw
# --- Infrastructure ---
runner main from "Tinkwell.Runner.Grpc.dll" {
    runlet store from "Tinkwell.Runlet.Store.dll" {
        storage = "memory"
    }
    runlet events from "Tinkwell.Runlet.Events.dll";
    runlet measures from "Tinkwell.Runlet.Measures.dll";
    runlet signals from "Tinkwell.Runlet.Signals.dll";
    runlet measure-events from "Tinkwell.Runlet.MeasureEvents.dll";
}

runner background from "Tinkwell.Runner.Headless.dll" {
    runlet actions from "Tinkwell.Runlet.Actions.dll";
    runlet coap from "Tinkwell.Runlet.Coap.dll";
}

# --- Measures ---
measure voltage {
    quantity = "Electric Potential"
    unit = "Volt"
}

measure current {
    quantity = "Electric Current"
    unit = "Ampere"
}

measure power {
    quantity = "Power"
    unit = "Watt"
    value = (voltage * current)
}

# --- Signals ---
signal high-power when (power > 1000) for "5 seconds" {
    severity = warning
}

# --- CoAP ---
coap sensors {
    resource "/measures/+" {
        on get {
            bind measure {
                name = (segment(path, -1))
            }
        }
        on post {
            bind measure {
                name = (segment(path, -1))
            }
        }
    }
}

# --- Actions ---
action log-alert when high-power {
    source = signals
    do log {
        message = (format("Power alert: {Name}"))
    }
}
```

---

## Configuration File Syntax

The `.tw` format is a block-structured configuration language.
It is **not** JSON, YAML, or TOML — it is purpose-built for Tinkwell with a clean, minimal syntax.

### Blocks

A block has a **type**, a **name**, optional **modifiers**, and either a **body** (enclosed in `{ }`) or a **semicolon** (for empty blocks):

```tw
type name [modifier value ...] {
    # body: properties and/or nested blocks
}

type name [modifier value ...];   # empty block (no body)
```

Examples:

```tw
runner main from "Tinkwell.Runner.Grpc.dll" {
    runlet store from "Tinkwell.Runlet.Store.dll";
}

signal alert when (temperature > 80);
```

### Properties

Inside a block body, **properties** are key-value pairs separated by `=`:

```tw
measure temperature {
    quantity = "Temperature"
    unit = "DegreeCelsius"
    minimum = -40
    maximum = 85
}
```

There is no comma or semicolon between properties — each property sits on its own line (or they can be separated by whitespace).

### Value Types

Properties and modifiers accept these value types:

| Syntax | Type | Example |
|--------|------|---------|
| `"quoted string"` | String | `"hello world"` |
| `unquoted-identifier` | String (syntactic sugar) | `Temperature`, `memory`, `fired` |
| `42`, `-10`, `3.14` | Number (integer or decimal) | `42`, `3.14` |
| `true`, `false` | Boolean | `true` |
| `(expression)` | Expression (evaluated at runtime) | `(voltage * current)` |
| `@"expression"` | Expression (verbatim string form) | `@"(a + b) / 2"` |
| `$"template {{var}}"` | Interpolated string (resolved at parse time) | `$"{{env}}-config"` |

**Unquoted strings** are a convenience — you can write `unit = Volt` instead of `unit = "Volt"`.
An unquoted identifier must start with a letter or underscore and may contain letters, digits, hyphens, and underscores.
The keywords `true` and `false` are always interpreted as booleans.

**Parenthesized expressions** `(...)` are evaluated at runtime using the NCalc expression engine.
They have access to variables from the surrounding context (measure values, event properties, request data, etc. — see [Expressions](#expressions) for what's available where).
Inside parentheses you can use arithmetic (`+`, `-`, `*`, `/`), comparisons (`>`, `<`, `==`, `!=`, `>=`, `<=`), logical operators (`and`, `or`, `not`), function calls, and single-quoted strings for literal arguments: `(segment(path, -1))`, `(json_value(payload, 'temperature'))`.

**Verbatim expression strings** `@"..."` are an alternative way to write expressions when parentheses would be awkward.
The content is treated as an expression.
Use `\"` to escape quotes inside.

### Modifiers

Modifiers appear between the block name and the opening brace (or semicolon).
They are keyword-value pairs:

```tw
runner main from "Tinkwell.Runner.Grpc.dll" { ... }
signal alert when (temp > 80) until (temp < 70) for "5 seconds";
do mqtt-publish from "Tinkwell.Actions" { ... }
```

Common modifiers include `from`, `when`, `until`, `for`, and `if`.
Each block type defines which modifiers it accepts.

### Comments

Lines starting with `#` or `//` are comments.
Inline comments with `//` are also supported:

```tw
# This is a full-line comment
// This is also a comment

measure temperature {
    quantity = Temperature  // inline comment
}
```

### Includes

Use `include` at the top of a file to inline another `.tw` file:

```tw
include "defaults.tw"
include "measures.tw"

runner main from "Tinkwell.Runner.Grpc.dll" { ... }
```

Includes must appear **before** any blocks.
Paths are relative to the including file.

### Variables and Interpolation

The `set` directive defines a variable at parse time:

```tw
set env = production
set broker_host = "192.168.1.100"
```

Use `$"{{variable}}"` (double curly braces) to interpolate variables into strings:

```tw
set env = production

measure status {
    description = $"Environment: {{env}}"
}
```

Interpolation happens at **parse time** — before any runtime evaluation.
It uses the Liquid template syntax.

### Templates

Templates let you define reusable block fragments:

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
}
```

The `using template-name` modifier on a block causes the template body to be expanded inside the block.
The `@content` placeholder in the template is replaced by whatever you put inside the block body.

### Conditional Blocks

The `if` modifier conditionally includes or excludes a block:

```tw
set enable_mqtt = true

runner mqtt-host from "Tinkwell.Runner.Headless.dll" if (enable_mqtt) {
    runlet mqtt from "Tinkwell.Runlet.Mqtt.dll";
}
```

If the expression evaluates to false, the entire block is removed during preprocessing.
This is evaluated at parse time, not at runtime.

---

## Ensemble and Runners

### What Are Runners and Runlets?

Tinkwell runs as a tree of processes managed by a **coordinator**:

```
Coordinator (parent process)
  ├── Runner: grpc-main     (hosts store, events, measures, signals)
  ├── Runner: background     (hosts actions, coap, mqtt)
  └── ...
```

- The **coordinator** reads the `.tw` configuration, launches each runner as a child process, and monitors them.
- A **runner** is an independent OS process.
  It hosts one or more runlets.
- A **runlet** is a service running inside a runner (state store, event bus, measures engine, etc.).

### Why Split Into Multiple Runners?

Splitting runlets into separate runners provides **fault isolation**: if one runner crashes, only its runlets are affected.
The coordinator can restart failed runners independently.
It also allows **independent monitoring** of each process.

However, splitting adds complexity and IPC overhead.
For most setups, **two runners are sufficient**:

1. A **gRPC runner** for services that expose network APIs (store, events, measures, signals).
2. A **headless runner** for background tasks (actions, CoAP, MQTT).

You can also put everything in a single runner if simplicity is preferred.
In the examples throughout this guide, we use a two-runner layout unless otherwise noted.

### Runner Types

| Runner | Assembly | Purpose |
|--------|----------|---------|
| gRPC | `Tinkwell.Runner.Grpc.dll` | HTTP/2 server for runlets that expose gRPC services (store, events, measures, signals, measure-events) |
| Headless | `Tinkwell.Runner.Headless.dll` | Background worker for runlets that don't need network endpoints (actions, CoAP, MQTT) |

Syntax:

```tw
runner <name> from "<runner-assembly>" {
    runlet <name> from "<runlet-assembly>" {
        # optional settings
    }
}
```

The `from` modifier is **required** on both `runner` and `runlet` blocks.

### Runlet Catalog

| Runlet | Assembly | Runner Type | Dependencies | Purpose |
|--------|----------|-------------|--------------|---------|
| `store` | `Tinkwell.Runlet.Store.dll` | gRPC | None | Key-value state store |
| `events` | `Tinkwell.Runlet.Events.dll` | gRPC | None | Event bus (publish/subscribe) |
| `measures` | `Tinkwell.Runlet.Measures.dll` | gRPC | Store service | Measure definitions, values, derived measures |
| `signals` | `Tinkwell.Runlet.Signals.dll` | gRPC | Measures (same runner, declared after), Events service | Condition-based event firing |
| `measure-events` | `Tinkwell.Runlet.MeasureEvents.dll` | gRPC | Measures (same runner, declared after), Events service | Bridges every measure value change to an event |
| `actions` | `Tinkwell.Runlet.Actions.dll` | Headless | Events service | Event-driven action handlers |
| `coap` | `Tinkwell.Runlet.Coap.dll` | Headless | Events, Measures, Store services | CoAP UDP server with binding chains |
| `mqtt` | `Tinkwell.Runlet.Mqtt.dll` | Headless | Events service | MQTT client with binding chains |
| `event-persistence` | `Tinkwell.Runlet.EventPersistence.dll` | Headless | Events (same runner, declared after) | Persists events to SQLite |
| `mqtt-server` | `Tinkwell.Runlet.MqttServer.dll` | Headless | None | Minimal MQTT broker for local development |

### Declaration Order

The coordinator starts runners **in the order they are declared** and waits for each to report ready before starting the next.
Within a runner, runlets are loaded in declaration order.

Rules:

1. `store` and `events` must be ready before any runlet that depends on them.
   Declare their runners first.
2. `signals` must be in the **same runner** as `measures` and declared **after** it.
3. `measure-events` must be in the **same runner** as `measures` and declared **after** it.
4. `event-persistence` must be in the **same runner** as `events` and declared **after** it.
5. If using both `mqtt-server` and `mqtt` in the same runner, declare `mqtt-server` **before** `mqtt`.

### Simplified Single-Runner Layout

For simplicity, the rest of this guide uses a compact two-runner layout:

```tw
runner main from "Tinkwell.Runner.Grpc.dll" {
    runlet store from "Tinkwell.Runlet.Store.dll" {
        storage = "memory"
    }
    runlet events from "Tinkwell.Runlet.Events.dll";
    runlet measures from "Tinkwell.Runlet.Measures.dll";
    runlet signals from "Tinkwell.Runlet.Signals.dll";
    runlet measure-events from "Tinkwell.Runlet.MeasureEvents.dll";
}

runner background from "Tinkwell.Runner.Headless.dll" {
    runlet actions from "Tinkwell.Runlet.Actions.dll";
    runlet coap from "Tinkwell.Runlet.Coap.dll";
    runlet mqtt from "Tinkwell.Runlet.Mqtt.dll";
}
```

The `main` runner hosts all gRPC services.
The `background` runner handles actions and protocol integrations.
Configuration blocks for measures, signals, actions, CoAP, and MQTT follow after the runner declarations in the same file.

---

## Measures

Measures are named values that your system tracks — sensor readings, computed metrics, status labels.
They are the foundation of Tinkwell: signals evaluate conditions against measures, derived measures compute values from other measures, and integrations read and write measures.

### Defining a Measure

```tw
measure <name> {
    # properties
}
```

The block name is the measure's unique identifier.
It must be unique across all measures in the configuration.

### Measure Properties

| Property | Required | Type | Description |
|----------|----------|------|-------------|
| `quantity` | No | String | Physical quantity type (e.g. `"Temperature"`, `"Pressure"`, `"Ratio"`). Defaults to `"Scalar"`. See [Units of Measure](#units-of-measure). |
| `unit` | No | String | Unit within the quantity type (e.g. `"DegreeCelsius"`, `"Volt"`). See [Units of Measure](#units-of-measure). |
| `minimum` | No | Number | Lower bound. Values outside this range are rejected. |
| `maximum` | No | Number | Upper bound. Values outside this range are rejected. |
| `precision` | No | Number (integer) | Number of decimal places for rounding. |
| `ttl` | No | Number | Time-to-live in seconds. The measure value expires after this duration if not updated. Must be greater than 0. |
| `value` | No | Number, String, or Expression | Initial value, constant value, or derived expression. See below. |
| `const` | No | Boolean | If `true`, the value cannot be changed after initialization. Requires `value` to be set. |
| `description` | No | String | Human-readable description. |
| `category` | No | String | Grouping category (e.g. `"environment"`, `"electrical"`). |
| `tags` | No | String | Comma-separated tags (e.g. `"indoor, hvac, sensor"`). |

### Plain Measures

A measure without a `value` property is a **plain measure**.
Its value is set externally — by a [CoAP request](#coap-integration), an [MQTT message](#mqtt-integration), an [action handler](#actions) (e.g. `update-measure`), or an API call:

```tw
measure temperature {
    quantity = Temperature
    unit = DegreeCelsius
    minimum = -40
    maximum = 125
    precision = 1
}
```

### Constant Measures

A measure with `const = true` cannot be modified after creation.
It must have a `value`:

```tw
measure firmware-version {
    value = 42
    const = true
}

measure pi {
    value = 3.14159
    precision = 5
    const = true
}
```

Constants cannot use expressions as their value.

### Derived Measures

A measure whose `value` is an **expression** is a **derived measure**.
It is automatically recalculated whenever any of its dependencies change:

```tw
measure power {
    quantity = Power
    unit = Watt
    value = (voltage * current)
}

measure avg-temp {
    quantity = Temperature
    unit = DegreeCelsius
    value = @"(indoor_temp + outdoor_temp) / 2"
}
```

Inside a derived expression, you can reference any other measure by name.
The system automatically detects dependencies and recalculates in the correct order (topological sort).
Circular dependencies are detected and rejected.
If an expression fails at runtime, the [error handling policy](#error-handling-in-derived-measures) determines what happens.

You can also use [built-in functions](#useful-functions):

```tw
measure temp-status {
    value = (if(temperature > 80, 'critical', if(temperature > 60, 'warning', 'normal')))
}
```

### String Measures

If a measure's `value` is a plain string (not an expression), it becomes a string-type measure:

```tw
measure label {
    value = "fixed-label"
}
```

### Units of Measure

Tinkwell uses the [UnitsNet](https://github.com/angularsen/UnitsNet) library for physical quantities and units.
A full list of supported quantities and units is available in the [Units Reference](units.md).

When specifying `quantity` and `unit`, use the names as listed in the reference.
For example:

| quantity | unit | Measures |
|----------|------|----------|
| `Temperature` | `DegreeCelsius`, `DegreeFahrenheit`, `Kelvin` | Room temperature |
| `ElectricPotential` | `Volt`, `Millivolt` | Voltage readings |
| `ElectricCurrent` | `Ampere`, `Milliampere` | Current measurements |
| `Pressure` | `Pascal`, `Bar`, `PoundForcePerSquareInch` | Pressure sensors |
| `RelativeHumidity` | `Percent` | Humidity sensors |
| `Speed` | `MeterPerSecond`, `KilometerPerHour` | Wind speed |
| `Power` | `Watt`, `Kilowatt` | Power consumption |
| `Ratio` | `Percent`, `DecimalFraction` | CPU load, battery level |

If no `quantity` is specified, the measure defaults to `Scalar` (dimensionless).

### Quantity and Unit Naming

Quantity and unit names are **case-insensitive** and support multiple formats:

```tw
# All of these are equivalent:
quantity = "Temperature"
quantity = "temperature"
quantity = "Electric Potential"
quantity = "electric-potential"
quantity = "electric_potential"
quantity = "ElectricPotential"
```

The parser normalizes all names to PascalCase internally.

### Runlet Dependencies (Measures)

The `measures` runlet requires:
- **Store service** — to persist definitions and values.

The store runner must be declared before the measures runner and be fully started before measures can register.

---

## Signals

Signals define conditions that, when met, fire events.
They watch measure values and trigger when thresholds are crossed.
Signals can include hysteresis to prevent flapping and debounce timers to filter out transient spikes.

### Defining a Signal

```tw
signal <name> when (<condition>) [until (<reset-condition>)] [for <duration>] {
    # optional properties
}
```

The block name is the signal's unique identifier.

### Top-Level Signals

A top-level signal references measures directly by name:

```tw
signal overheat when (temperature > 80) {
    severity = critical
    channel = ops
}
```

### Inline Signals (Inside a Measure)

Signals can be defined inside a `measure` block.
The special keyword `value` refers to the enclosing measure's current value:

```tw
measure temperature {
    quantity = Temperature
    unit = DegreeCelsius

    signal hot when (value > 50);
    signal critical when (value > 100) for 5;
}
```

This is syntactic sugar — the parser replaces `value` with the measure name (`temperature`) automatically.
The above is equivalent to:

```tw
signal hot when (temperature > 50);
signal critical when (temperature > 100) for 5;
```

### The `when` Clause

The `when` modifier is **required**.
It is a boolean expression that determines when the signal triggers.
The expression can reference any measure by name:

```tw
signal combined-alert when (temperature > 80 or pressure > 200);
```

### The `until` Clause (Hysteresis)

The optional `until` clause suppresses re-firing after a signal has been activated.
The signal stays in an "active" state until the `until` condition becomes true, then resets to idle:

```tw
signal overheat when (temperature > 80) until (temperature < 70) {
    severity = critical
}
```

Without `until`, the signal fires once and immediately returns to idle (ready to fire again if the condition is still true on the next evaluation cycle).

### The `for` Clause (Debounce / Hold Time)

The optional `for` clause requires the `when` condition to remain true for a specified duration before firing.
If the condition becomes false during the waiting period, the signal resets without firing.

Three formats are supported:

```tw
# Numeric literal (seconds)
signal alert when (pressure > 100) for 10;

# String (parsed by UnitsNet — see the Units Reference for abbreviations)
signal overheat when (temp > 80) for "5 seconds";
signal fast-alert when (vibration > 50) for "500 ms";

# Expression (evaluates to seconds at runtime)
signal dynamic-alert when (temp > 80) for (cycle_time / 10);
```

You can also use the [`quantity()` function](#the-quantity-function) in duration expressions for unit conversion: `for (quantity(500, 'ms'))`.

### Signal Properties

Additional properties in the signal body are passed as payload when the signal event is published:

```tw
signal overheat when (temperature > 80) {
    severity = critical
    channel = ops
    description = "Temperature exceeded safe threshold"
}
```

These properties become key-value pairs in the [event's](#the-event-model) `Payload` dictionary.
You can use them to trigger specific [actions](#actions) or provide context to handlers.

### Signal State Machine

Each signal instance follows this lifecycle:

1. **Idle** — waiting for `when` to become true.
2. **Pending** — `when` is true and the `for` timer is running.
   Returns to Idle if the condition clears.
3. **Fired** — the signal event is emitted (source=`"signals"`, verb=`Fired`, name=signal name).
   This event can be consumed by [actions](#actions) using `source = signals` and `verb = fired`.
   If `until` is defined, transitions to Active; otherwise returns to Idle.
4. **Active** — the signal is suppressed.
   When the `until` condition becomes true, returns to Idle.

### Runlet Dependencies (Signals)

The `signals` runlet requires:
- **Measures** — must be in the **same runner** and declared **before** signals.
- **Events service** — to publish signal events (discovered via service discovery).

---

## Events

Events are the communication backbone of Tinkwell.
Signals fire events, measure changes produce events (via the bridge), and actions consume events.
The event bus is a fire-and-forget publish/subscribe system.

### The Event Model

Every event follows a Subject-Verb-Object pattern:

| Field | Type | Description |
|-------|------|-------------|
| `Source` | String | Who produced the event (e.g. `"signals"`, `"measures"`, `"coap"`, `"mqtt"`, `"actions"`) |
| `Verb` | Enum/String | What happened (see below) |
| `Name` | String | Entity name (signal name, measure name, etc.) |
| `Object` | String (optional) | Additional value or target |
| `CorrelationId` | String (optional) | Tracks causal chains across subsystems |
| `Timestamp` | DateTime | When the event occurred (UTC) |
| `Payload` | Key-Value map | Arbitrary extra properties |

### Well-Known Verbs

| Verb | Typical Source | Meaning |
|------|----------------|---------|
| `Fired` | Signals | A signal condition was met |
| `Changed` | Measures, MQTT, CoAP | A value was updated |
| `Created` | Actions, Store | A new entity was created |
| `Deleted` | Actions, Store | An entity was removed |
| `Expired` | Store | A TTL-based entry expired |
| `Started` | Coordinator | A runner started |
| `Stopped` | Coordinator | A runner stopped |
| `Failed` | Actions, Error policies | An operation failed |
| `Custom` | Any | Free-form custom verb |

### Measure-Events Bridge

The `measure-events` runlet bridges **all** measure value changes to the event bus.
Every time a measure value is updated, it publishes:

```
Source = "measures", Verb = Changed, Name = <measure-name>, Object = <new-value>
```

This is useful for triggering [actions](#actions) whenever a measure changes.
To enable it, add the runlet to the same runner as `measures` (see [Declaration Order](#declaration-order)):

```tw
runlet measure-events from "Tinkwell.Runlet.MeasureEvents.dll";
```

### Event Persistence

The `event-persistence` runlet records all events to a local SQLite database for later analysis:

```tw
runlet event-persistence from "Tinkwell.Runlet.EventPersistence.dll" {
    db-path = "events.db"
    batch-size = 100
    flush-interval = 1
}
```

It must be in the **same runner** as `events` and declared **after** it.

### Runlet Dependencies (Events)

The `events` runlet has no dependencies — it should be one of the first runlets started.

---

## Actions

Actions subscribe to the [event bus](#events) and execute configurable handlers in response to matching events.
Use actions to log alerts, publish new events, [update measures](#measures), modify store entries, send [MQTT messages](#mqtt-integration), or make [CoAP requests](#coap-integration).
Actions support configurable [error handling with retry](#error-handling-in-actions).

### Defining an Action

```tw
action <name> [when <event-name>] {
    [source = <filter>]
    [verb = <filter>]

    do <handler-name> [from "<assembly>"] {
        <param> = <value>
        ...
    }
}
```

The block name is the action's unique identifier.
Multiple actions can be defined; they are all evaluated for every event.

### Event Filters

Filters narrow which events trigger the action.
All filters are optional — an action with no filters matches **every** event.

| Filter | Location | Description |
|--------|----------|-------------|
| `when <name>` | Modifier | Matches `EventEnvelope.Name` (case-insensitive). Written after the action name. |
| `source = <value>` | Property | Matches `EventEnvelope.Source`. |
| `verb = <value>` | Property | Matches the event verb. |

```tw
# Fires on any event named "high-temperature" from any source
action alert when high-temperature { ... }

# Fires on any "fired" event from signals
action log-signals {
    source = signals
    verb = fired
    ...
}

# Fires on absolutely every event
action log-everything { ... }
```

### Handler Blocks (`do`)

Each `do` block specifies a handler to execute.
Multiple `do` blocks per action are allowed — they execute in order:

```tw
action alert when high-temperature {
    do log {
        message = (format("Temperature alert: {Name}"))
    }
    do create-event {
        source = actions
        verb = fired
        name = (format("alert.{Name}"))
    }
}
```

The `from` modifier specifies which assembly contains the handler.
It is **optional** for built-in handlers:

- Handlers in `Tinkwell.Runlet.Actions` (like `log` and `create-event`) — `from` not needed.
- Handlers in `Tinkwell.Actions` (like `update-measure`, `update-entry`, `mqtt-publish`, `coap-request`) — `from` not needed (auto-resolved).
- Custom handlers — require `from "YourAssembly"`.

### Built-In Handlers

#### `log`

Writes a message to the application log.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `message` | Yes | — | The log message. Supports expressions with `format()`. |
| `level` | No | `information` | Log level: `trace`, `debug`, `information`, `warning`, `error`, `critical`. |

```tw
do log {
    message = (format("Signal {Name} fired from {Source}"))
    level = warning
}
```

#### `create-event`

Publishes a new event to the event bus, preserving the original `CorrelationId`.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `source` | Yes | Event source identifier. |
| `verb` | Yes | Event verb (`fired`, `changed`, `created`, etc.). |
| `name` | Yes | Event name. |
| `object` | No | Event value/target. |

```tw
do create-event {
    source = actions
    verb = fired
    name = (format("processed.{Name}"))
    object = (Object)
}
```

### External Action Handlers (`Tinkwell.Actions`)

These handlers live in the `Tinkwell.Actions` assembly.
The `from` modifier can be omitted.

#### `mqtt-publish`

Publishes a message to an MQTT broker.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `topic` | Yes | — | MQTT topic. |
| `payload` | Yes | — | Message payload. |
| `broker` | No | `localhost` | Broker hostname or IP. |
| `port` | No | `1883` | Broker port. |
| `qos` | No | `0` | Quality of service (0, 1, or 2). |
| `retain` | No | `false` | Retain flag. |
| `client-id` | No | Auto-generated | MQTT client identifier. |

```tw
do mqtt-publish {
    topic = (format("alerts/{Name}"))
    payload = (format("{Name} fired at {Timestamp}"))
    broker = "192.168.1.100"
}
```

#### `coap-request`

Sends a CoAP request to a UDP endpoint.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `path` | Yes | — | CoAP URI path (e.g. `/sensor/temperature`). |
| `method` | No | `post` | CoAP method: `post`, `put`, or `delete`. |
| `payload` | No | — | Request payload. |
| `host` | No | `localhost` | Target hostname or IP. |
| `port` | No | `5683` | Target UDP port. |
| `timeout` | No | `5` | Response timeout in seconds. |

```tw
do coap-request {
    path = "/device/restart"
    method = post
    payload = (format("{Name}"))
    host = "192.168.1.50"
}
```

#### `update-measure`

Sets a measure's current value.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `name` | Yes | Measure name. |
| `value` | Yes | New value (numeric or string). |

```tw
do update-measure {
    name = pump-state
    value = restarting
}
```

#### `create-measure`

Creates a new measure definition.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `name` | Yes | Measure name. |
| `quantity` | No | Quantity type (e.g. `"Temperature"`). |
| `unit` | No | Unit (e.g. `"DegreeCelsius"`). |
| `value` | No | Initial numeric value. |

#### `update-entry`

Writes a key-value entry to the state store.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `bucket` | Yes | Bucket identifier. |
| `key` | Yes | Entry key. |
| `value` | Yes | Entry value. |
| `namespace` | No | Key namespace. |
| `ttl` | No | Time-to-live in seconds. |

```tw
do update-entry {
    bucket = history
    key = (format("voltage.{CorrelationId}"))
    value = (Object)
    ttl = 3600
}
```

#### `delete-entry`

Deletes an entry from the state store.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `bucket` | Yes | Bucket identifier. |
| `key` | Yes | Entry key. |
| `namespace` | No | Key namespace. |

### Expression Variables in Actions

When an action fires, the triggering event's properties become expression variables:

| Variable | Type | Description |
|----------|------|-------------|
| `Source` | String | Event source (e.g. `"signals"`, `"measures"`) |
| `Verb` | String | Lowercase verb (e.g. `"fired"`, `"changed"`) |
| `Name` | String | Event name |
| `Object` | String or null | Event value/target |
| `CorrelationId` | String or null | Correlation ID |
| `Timestamp` | DateTime | Event timestamp (UTC) |

Additionally, all entries from the event's `Payload` dictionary are available as variables.
Event properties take precedence over payload keys on name conflict.

### The `format()` Function

Use `format()` for runtime string interpolation:

```tw
message = (format("Temperature alert: {Name} = {Object}"))
```

Placeholders in curly braces reference the expression variables listed above.
Unknown placeholders are left as-is.
This is different from [`$"..."` interpolation](#variables-and-interpolation) which happens at parse time — `format()` runs at runtime against the triggering event.

### Runlet Dependencies (Actions)

The `actions` runlet requires:
- **Events service** — to subscribe to events and publish new ones.

---

## CoAP Integration

### CoAP Overview

[CoAP (Constrained Application Protocol)](https://tools.ietf.org/html/rfc7252) is a lightweight UDP-based protocol designed for IoT devices.
Tinkwell's CoAP runlet runs UDP servers that accept CoAP requests and process them through a pluggable **binding chain** — a sequence of handlers that read/write [measures](#measures), publish [events](#events), forward to other servers, or bridge to [MQTT](#mqtt-integration).
Bindings support configurable [error handling with retry](#error-handling-in-coap-bindings).

### Defining a CoAP Server

```tw
coap <name> {
    port = 5683

    resource "<path-pattern>" {
        on <verb> [when (<expression>)] {
            bind <name> [from "<assembly>"] [when (<expression>)] {
                # binding parameters
            }
        }
    }
}
```

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `port` | No | `5683` | UDP port to listen on |
| `max-concurrent-requests` | No | `100` | Maximum requests processed concurrently. Each incoming datagram acquires a semaphore slot before handler execution begins. |
| `max-pending-requests` | No | `200` | Maximum requests waiting for a concurrency slot. Excess datagrams are rejected with 5.03 Service Unavailable. Set to `0` to disable (requests wait indefinitely). |

Multiple `coap` blocks define multiple servers (on different ports).
Server names must be unique.

### Path Patterns

Resource paths support wildcards:

| Pattern | Description | Example Match |
|---------|-------------|---------------|
| `/sensor/temperature` | Exact match | `/sensor/temperature` only |
| `/sensor/+` | `+` matches exactly one segment | `/sensor/temperature`, `/sensor/pressure` |
| `/sensor/#` | `#` matches zero or more trailing segments | `/sensor`, `/sensor/a`, `/sensor/a/b/c` |

### Verb Blocks (`on`)

Inside a resource, `on <verb>` blocks group bindings by CoAP method:

| Verb | CoAP Method |
|------|-------------|
| `get` | GET — read data |
| `post` | POST — create data |
| `put` | PUT — create or update data |
| `delete` | DELETE — remove data |

Multiple `on` blocks for the same verb are allowed — all matching blocks execute in order.
Each `on` block can optionally have a `when` filter.

### Binding Blocks (`bind`)

Inside an `on` block, `bind` blocks execute in order.
Each binding receives the request context and can read/write measures, publish events, forward to other protocols, etc.

```tw
bind <binding-name> [from "<assembly>"] [when (<expression>)] {
    # parameters
    with <label> {
        # nested parameters (e.g. event payload entries)
    }
}
```

The `from` modifier is **optional** for built-in bindings — they default to the `Tinkwell.Integrations` assembly.

If a binding produces an output (e.g. a measure value for GET), the **last** non-null output from the binding chain becomes the CoAP response body.

### When Filters

Filters can appear at two levels:

**Verb-level** — skips the entire `on` block if the expression is falsy:

```tw
on post when (query == "auth=secret") {
    bind store { ... }
}
```

**Binding-level** — skips one specific binding while others in the same block still execute:

```tw
on post {
    bind measure {
        name = (segment(path, -1))
    }
    bind event when (json_value(payload, '$.severity') == 'critical') {
        source = coap
        verb = alert
        name = (segment(path, -1))
    }
}
```

Both levels compose: the verb filter runs first, then each binding filter individually.

### Built-In Bindings (CoAP)

#### `measure`

Reads and writes measure values.

| Method | Behavior | Output |
|--------|----------|--------|
| GET | Reads current value | Text (default) or binary IEEE 754 float |
| POST/PUT | Sets value from request payload | None |
| DELETE | No-op | None |

| Parameter | Required | Description |
|-----------|----------|-------------|
| `name` | Yes | Measure name (literal or expression) |

```tw
bind measure {
    name = (segment(path, -1))
}
```

#### `event`

Publishes an event to the event bus.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `source` | Yes | Event source identifier |
| `verb` | Yes | Event verb (`changed`, `created`, `fired`, `custom:xxx`) |
| `name` | Yes | Event name |
| `object` | No | Event value |

Nested `with <label> { ... }` blocks add entries to the event's `Payload` dictionary:

```tw
bind event {
    source = coap
    verb = changed
    name = (segment(path, -1))
    with payload {
        device = (segment(path, 1))
        raw = (payload)
    }
}
```

#### `store`

CRUD operations on the state store.

| Method | Behavior | Output |
|--------|----------|--------|
| GET | Reads entry | Text or JSON |
| POST | Creates entry | None |
| PUT | Creates or updates (upsert) | None |
| DELETE | Removes entry | None |

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `bucket` | Yes | — | Bucket ID |
| `key` | Yes | — | Entry key (expression or literal) |
| `namespace` | No | `""` | Key namespace |
| `value` | No | Request payload | Value for POST/PUT |
| `ttl` | No | — | Time-to-live in seconds |

### Outbound Bindings (CoAP)

These bindings send data **out** to other services.
They can be used from CoAP verb blocks (and MQTT verb blocks) to tunnel data between protocols.

#### `coap` (outbound)

Sends a CoAP request to another CoAP server.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `path` | Yes | — | Target CoAP URI path |
| `method` | No | `post` | CoAP method: `post`, `put`, or `delete` |
| `host` | No | `localhost` | Target hostname or IP |
| `port` | No | `5683` | Target UDP port |
| `timeout` | No | `5` | Response timeout in seconds |

The payload is taken from the incoming request's payload.

#### `mqtt` (outbound)

Publishes a message to an MQTT broker.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `topic` | Yes | — | MQTT topic to publish to |
| `broker` | No | `localhost` | Broker hostname or IP |
| `port` | No | `1883` | Broker port |
| `qos` | No | `0` | Quality of service (0, 1, or 2) |
| `retain` | No | `false` | Retain flag |
| `client-id` | No | Auto-generated | MQTT client identifier |

The payload is taken from the incoming request's payload.

### Expression Variables in CoAP

Expressions inside `on` and `bind` blocks have access to:

| Variable | Description |
|----------|-------------|
| `path` | Request URI path (e.g. `/sensor/temperature`) |
| `query` | Query string (e.g. `"auth=secret"`) — empty string if none |
| `payload` | Request body as a string — empty string for GET |
| `method` | HTTP-like method string: `GET`, `POST`, `PUT`, `DELETE` |

Commonly used functions:

- `segment(path, N)` — splits by `/` and returns segment at index N. Negative indexes count from the end (`-1` = last).
- `json_value(payload, 'key')` — extracts a value from JSON payload.

### Response Codes

| Code | Meaning |
|------|---------|
| 2.01 Created | POST with no binding output |
| 2.02 Deleted | DELETE with no binding output |
| 2.04 Changed | PUT with no binding output |
| 2.05 Content | Binding returned output body |
| 4.00 Bad Request | Invalid parameter in a binding |
| 4.04 Not Found | No matching resource pattern |
| 4.05 Method Not Allowed | No `on` block for this verb |
| 5.00 Internal Server Error | Unhandled exception |

### CoAP Examples

#### Sensor ingestion and readback

Accept POST to write sensor values and GET to read them back:

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
                source = coap
                verb = changed
                name = (segment(path, -1))
                object = (payload)
            }
        }
    }
}
```

A POST to `/sensor/temperature` with body `23.5` sets the `temperature` measure to `23.5` and publishes a `changed` event.
A GET to `/sensor/temperature` returns the current value.

#### State store CRUD

Full CRUD operations:

```tw
coap storage {
    port = 5684

    resource "/store/+" {
        on get {
            bind store {
                bucket = default
                key = (segment(path, -1))
            }
        }
        on post {
            bind store {
                bucket = default
                key = (segment(path, -1))
                ttl = 3600
            }
        }
        on put {
            bind store {
                bucket = default
                key = (segment(path, -1))
            }
        }
        on delete {
            bind store {
                bucket = default
                key = (segment(path, -1))
            }
        }
    }
}
```

#### CoAP to CoAP forwarding

Forward sensor data from one CoAP server to another:

```tw
coap gateway {
    port = 5683

    resource "/sensor/+" {
        on post {
            # Store locally
            bind measure {
                name = (segment(path, -1))
            }
            # Forward to another CoAP server
            bind coap {
                path = (path)
                method = post
                host = "192.168.1.200"
                port = 5683
            }
        }
    }
}
```

When a device POSTs to `/sensor/temperature`, the gateway writes the measure locally and forwards the same payload to another CoAP server at `192.168.1.200`.

#### CoAP to MQTT bridging

Forward CoAP sensor data to an MQTT broker:

```tw
coap sensor-bridge {
    resource "/sensor/+" {
        on post {
            bind measure {
                name = (segment(path, -1))
            }
            bind mqtt {
                topic = (format("sensors/{0}", segment(path, -1)))
                broker = "mqtt-broker.local"
                qos = 1
            }
        }
    }
}
```

When a CoAP POST arrives at `/sensor/temperature`, the payload is written as a measure value and also published to the MQTT topic `sensors/temperature`.

### Runlet Dependencies (CoAP)

The `coap` runlet requires:
- **Events service** — for event bindings.
- **Measures service** — for measure bindings.
- **Store service** — for store bindings.

All are discovered via service discovery.
Ensure those runners are started before the CoAP runner.

---

## MQTT Integration

### MQTT Overview

[MQTT (Message Queuing Telemetry Transport)](https://mqtt.org/) is a lightweight publish-subscribe protocol widely used in IoT.
Tinkwell's MQTT runlet connects to one or more MQTT brokers, subscribes to topics, and processes incoming messages through binding chains — the same model as [CoAP](#coap-integration).
Bindings can write [measures](#measures), publish [events](#events), forward to other brokers, or bridge to [CoAP](#coap-integration).
Bindings support configurable [error handling with retry](#error-handling-in-mqtt-bindings).

### Defining an MQTT Connection

```tw
mqtt <connection-name> {
    broker = "<hostname>"
    # optional settings...

    subscribe "<topic-filter>" {
        on message [when (<expression>)] {
            bind <name> [from "<assembly>"] [when (<expression>)] {
                # binding parameters
            }
        }
    }
}
```

Multiple `mqtt` blocks define multiple connections (to different brokers or the same broker with different settings).
Connection names must be unique.

### Connection Properties

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `broker` | **Yes** | — | Broker hostname or IP address |
| `port` | No | `1883` | Broker TCP port |
| `client-id` | No | `"tinkwell"` | MQTT client identifier |
| `username` | No | — | Broker username. Supports `%ENV_VAR%` expansion for secrets. |
| `password` | No | — | Broker password. Supports `%ENV_VAR%` expansion for secrets. |
| `retry-count` | No | `3` | Number of connection retry attempts |
| `retry-delay` | No | `2000` | Milliseconds between connection retries |
| `max-pending-messages` | No | `1000` | Maximum messages buffered before dropping. Incoming MQTT messages are queued in a bounded channel; when full, the oldest message is dropped. Set to `0` for an unbounded queue (not recommended in production). |

Environment variable expansion: `password = "%MQTT_PASSWORD%"` reads the value from the `MQTT_PASSWORD` environment variable at runtime.
You can also use [`set` variables and `$"..."` interpolation](#variables-and-interpolation) for broker addresses shared across multiple blocks.

### Subscribe Blocks

Each `subscribe` block declares a topic filter.
The filter uses standard MQTT wildcards:

- `+` matches exactly one topic level: `sensor/+` matches `sensor/temperature` but not `sensor/a/b`.
- `#` matches zero or more levels: `sensor/#` matches `sensor`, `sensor/a`, `sensor/a/b/c`.

Each `subscribe` block must contain at least one `on message` block.

### Verb Blocks (`on message`)

```tw
subscribe "sensor/+" {
    on message [when (<expression>)] {
        bind event { ... }
        bind measure { ... }
    }
}
```

The only supported verb for MQTT is `message`.
The optional `when` modifier filters messages based on an expression — if the expression is falsy, the entire block is skipped.

### Binding Blocks (MQTT)

Same syntax as CoAP bindings:

```tw
bind <binding-name> [from "<assembly>"] [when (<expression>)] {
    # parameters
    with <label> {
        # nested parameters
    }
}
```

The `from` modifier is optional for built-in bindings — they default to `Tinkwell.Integrations`.

### Built-In Bindings (MQTT)

The same bindings available in CoAP are available in MQTT:

#### `event`

Publishes an event to the event bus.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `source` | Yes | Event source identifier |
| `verb` | Yes | Event verb |
| `name` | Yes | Event name |
| `object` | No | Event value |

Nested `with <label> { ... }` blocks add payload entries.

#### `measure`

Writes the message payload as a measure value.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `name` | Yes | Measure name (expression or literal) |

#### `store`

Writes to the state store.
The message payload is used as the value if `value` is not specified.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `bucket` | Yes | — | Bucket ID |
| `key` | Yes | — | Entry key |
| `namespace` | No | `""` | Key namespace |
| `value` | No | Message payload | Value to store |
| `ttl` | No | — | Time-to-live in seconds |

### Outbound Bindings (MQTT)

#### `mqtt` (republish)

Publishes to another MQTT topic or broker — useful for topic remapping or broker bridging.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `topic` | Yes | — | Target topic |
| `broker` | No | `localhost` | Broker hostname or IP |
| `port` | No | `1883` | Broker port |
| `qos` | No | `0` | Quality of service |
| `retain` | No | `false` | Retain flag |
| `client-id` | No | Auto-generated | Client identifier |

#### `coap` (outbound)

Sends a CoAP request based on the MQTT message — bridges MQTT to CoAP.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `path` | Yes | — | Target CoAP URI path |
| `method` | No | `post` | CoAP method: `post`, `put`, or `delete` |
| `host` | No | `localhost` | Target hostname or IP |
| `port` | No | `5683` | Target UDP port |
| `timeout` | No | `5` | Response timeout in seconds |

### Expression Variables in MQTT

Expressions inside `on message` and `bind` blocks have access to:

| Variable | Description |
|----------|-------------|
| `topic` | Full MQTT topic (e.g. `"sensor/temperature"`) |
| `path` | Same as `topic` (alias for binding compatibility with CoAP) |
| `payload` | Raw message payload as a string |
| `method` | Always `"MESSAGE"` |

Commonly used functions:

- `segment(topic, N)` — splits the topic by `/` and returns the segment at index N. Negative indexes count from the end (`-1` = last).
- `json_value(payload, 'key')` — extracts a value from a JSON payload.
- `format(template)` — replaces `{Name}` placeholders from the expression context.

### MQTT Examples

#### Basic event publishing

Convert MQTT messages to Tinkwell events:

```tw
mqtt sensors {
    broker = "localhost"

    subscribe "sensor/+" {
        on message {
            bind event {
                source = mqtt
                verb = changed
                name = (segment(topic, -1))
                object = (payload)
            }
        }
    }
}
```

A message on `sensor/temperature` with payload `"23.5"` publishes event: `Source=mqtt, Verb=Changed, Name=temperature, Object=23.5`.

#### Write measures from MQTT

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

#### JSON payload extraction

```tw
mqtt devices {
    broker = "192.168.1.100"

    subscribe "device/+/telemetry" {
        on message {
            bind event {
                source = mqtt
                verb = changed
                name = (segment(topic, 1))
                object = (json_value(payload, 'value'))
                with payload {
                    unit = (json_value(payload, 'unit'))
                    device = (segment(topic, 1))
                }
            }
        }
    }
}
```

#### MQTT broker-to-broker bridging

Tunnel messages from one broker to another:

```tw
mqtt source-broker {
    broker = "broker-a.local"

    subscribe "factory/+" {
        on message {
            bind mqtt {
                topic = (format("replicated/{0}", segment(topic, -1)))
                broker = "broker-b.local"
                port = 1883
                qos = 1
            }
        }
    }
}
```

Messages on `factory/line1` at `broker-a.local` are republished as `replicated/line1` at `broker-b.local`.

#### MQTT to CoAP bridging

Forward MQTT messages to a CoAP server:

```tw
mqtt sensor-bridge {
    broker = "localhost"

    subscribe "sensor/+" {
        on message {
            bind coap {
                path = (format("/sensor/{0}", segment(topic, -1)))
                method = post
                host = "coap-gateway.local"
                port = 5683
            }
        }
    }
}
```

A message on `sensor/temperature` with payload `"23.5"` sends a CoAP POST to `coap://coap-gateway.local:5683/sensor/temperature` with body `23.5`.

#### Multiple brokers with authentication

```tw
mqtt warehouse {
    broker = "broker-a.local"

    subscribe "warehouse/+" {
        on message {
            bind event {
                source = mqtt
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
                source = mqtt
                verb = changed
                name = (segment(topic, 1))
                object = (payload)
            }
        }
    }
}
```

### Runlet Dependencies (MQTT)

The `mqtt` runlet requires:
- **Events service** — for event bindings (discovered via service discovery).

If you use measure or store bindings, those services must also be available.

---

## Protobuf Gateway

### Overview

The protobuf gateway runlet tunnels raw protobuf bytes from device-facing CoAP requests to backend gRPC services.
Devices POST serialized protobuf messages to a configurable URL pattern; the gateway discovers the target service, forwards the bytes using identity marshallers (zero deserialization), and returns the gRPC response.
MQTT devices can use the existing MQTT-to-CoAP bridge.

### Runlet Declaration

```tw
runner pb-host from "Tinkwell.Runner.Headless.dll" {
    runlet pb from "Tinkwell.Runlet.ProtobufGateway.dll" {
        port = 5684
    }
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `port` | int | 5684 | UDP port for the CoAP server |
| `name` | string | *(none)* | Runlet identity for matching `for` modifiers. When omitted, only `for "*"` profiles match. |
| `path` | string | *(coordinator config)* | Path to the `.tw` file with `protobuf-gateway` blocks |

### Access Profiles

Top-level `protobuf-gateway` blocks define access profiles that attach to a runlet by name:

```tw
protobuf-gateway device-fleet for "pb" match "/device/{service}/{method}" {
    allow "tinkwell.measures.*";
    allow "tinkwell.events.EventBus";
}

protobuf-gateway admin-tools for "pb" match "/admin/{service}/{method}" {
    allow "tinkwell.*";
}
```

#### Block structure

| Part | Position | Default | Description |
|------|----------|---------|-------------|
| Name | Block name | *(required)* | Profile label (for logging and future identity mapping) |
| `for` | Modifier | `"*"` | Target runlet name. `"*"` matches any protobuf gateway runlet. |
| `match` | Modifier | `"/{service}/{method}"` | Path template. Must contain `{service}` and `{method}`. |
| `allow` | Child | *(none = deny all)* | Service name patterns (glob-style). |

#### Whitelist behavior

- **No `allow` rules** — deny all (nothing gets through, secure default).
- **`allow "*";`** — all discovered services are permitted.
- **`allow "tinkwell.measures.*";`** — only services matching the glob prefix.
- Multiple `allow` rules are unioned.

#### Minimal configuration

When `for` and `match` are omitted, they default to `"*"` and `"/{service}/{method}"` respectively:

```tw
protobuf-gateway open-access {
    allow "*";
}
```

This matches any protobuf gateway runlet and accepts all services at the default path.

### Path Convention

Devices POST raw protobuf bytes with `Content-Format: application/octet-stream` (42).

Examples with default `match "/{service}/{method}"`:

- `POST /tinkwell.measures.Measures/Update` — update a measure
- `POST /tinkwell.store.StateStore/Get` — read from the store

Examples with `match "/rpc/{service}/{method}"`:

- `POST /rpc/tinkwell.measures.Measures/Update`
- `POST /rpc/tinkwell.events.EventBus/Publish`

Only POST is supported (matching the gRPC convention).
Other methods return 4.05 Method Not Allowed.

### Multiple Profiles on One Server

Multiple `protobuf-gateway` blocks targeting the same runlet share a single CoAP server.
Each block registers its own route based on its `match` pattern:

```tw
protobuf-gateway fleet for "pb" match "/device/{service}/{method}" {
    allow "tinkwell.measures.*";
}

protobuf-gateway admin for "pb" match "/admin/{service}/{method}" {
    allow "tinkwell.*";
}
```

If two blocks share the same `match` pattern, their `allow` rules are merged (union).
A warning is logged — the profiles are indistinguishable until identity/DTLS is implemented.

### Error Responses

| Condition | CoAP Response |
|-----------|---------------|
| Missing or malformed path | 4.00 Bad Request |
| Non-POST method | 4.05 Method Not Allowed |
| Service not in whitelist | 4.03 Forbidden |
| Service not found via discovery | 4.04 Not Found |
| gRPC Unavailable | 5.03 Service Unavailable |
| gRPC InvalidArgument | 4.00 Bad Request |
| gRPC NotFound | 4.04 Not Found |
| Other gRPC error | 5.00 Internal Server Error |

### Future Extensions

These are not implemented but syntactically compatible with the current block grammar:

- **Method-level filtering:** `allow "tinkwell.store.StateStore" with "Get";`
- **Identity mapping:** `identity "fleet-psk-key";` inside a profile block

### Dependencies

The protobuf gateway requires the target gRPC services to be discoverable via the coordinator.
No specific service dependency — it tunnels to whatever services are allowed by the profile.

---

## LwM2M Integration

### LwM2M Overview

[LwM2M (Lightweight M2M)](https://www.openmobilealliance.org/release/LightweightM2M/) is an OMA device management protocol built on CoAP.
It uses a standardized object/resource model where each sensor or actuator is identified by numeric IDs (e.g. object 3303 = Temperature, resource 5700 = Sensor Value).
Tinkwell's LwM2M runlet provides a server that handles device registration, reads, and writes, mapping LwM2M resources directly to Tinkwell measures.

### Defining an LwM2M Server

```tw
lwm2m <name> {
    port = 5684

    registration config {
        default-lifetime = 86400
        emit-events = true
    }

    object "<object-id>" {
        resource "<resource-id>" {
            measure = "<measure-name>"
            observable = true
        }
    }
}
```

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `port` | No | `5683` | UDP port to listen on |

Object and resource IDs must be quoted (e.g. `"3303"`) because they start with a digit.

### Registration

The server exposes a registration endpoint at `/rd` per OMA-TS-LightweightM2M_Transport-V1_1, Section 5.3:

| Operation | Method | Path | Description |
|-----------|--------|------|-------------|
| Register | POST | `/rd?ep=<name>&lt=<seconds>` | Client registers with endpoint name and lifetime |
| Update | POST | `/rd/<location>?lt=<seconds>` | Client refreshes its registration |
| Deregister | DELETE | `/rd/<location>` | Client leaves the server |

Expired registrations are purged automatically.
The payload of the Register request is an RFC 6690 link-format list of objects the client supports (e.g. `</3303/0>,</3304/0>`).

#### Registration options

Nested inside the `lwm2m` block as `registration config { ... }`:

| Property | Default | Description |
|----------|---------|-------------|
| `default-lifetime` | `86400` | Lifetime in seconds when the client does not specify `lt` |
| `emit-events` | `true` | Publish Tinkwell events on register/deregister |

### Object mappings

Each `object` block maps an LwM2M object ID to one or more resources.
Each `resource` block maps a single resource ID to a Tinkwell measure:

```tw
object "3303" {
    resource "5700" {
        measure = "temperature"
        observable = true
    }
    resource "5701" {
        measure = "temperature-unit"
    }
}
```

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `measure` | Yes | — | Name of the Tinkwell measure to write to |
| `observable` | No | `false` | Whether the resource supports CoAP Observe notifications |

### Read and Write operations

Clients (or the LwM2M server itself for Observe) read resources via GET on `/{objectId}/{instanceId}/{resourceId}`.
The response format is chosen by the request's Accept option:

| Accept | Content-Format | Description |
|--------|----------------|-------------|
| `text/plain` (0) | `text/plain` | Default; single value as text |
| `application/vnd.oma.lwm2m+tlv` (11542) | TLV | Binary Type-Length-Value encoding |
| `application/senml+json` (110) | SenML JSON | JSON array per RFC 8428 |

Instance-level reads (GET `/{objectId}/{instanceId}`) return all resources for that instance in TLV or SenML-JSON.

Write operations use PUT or POST on a resource path.
The server decodes the payload based on the Content-Format option (text/plain, TLV, or SenML-JSON) and updates both the internal resource store and the mapped Tinkwell measure.

### Supported IPSO objects

The following IPSO Smart Objects are predefined with their resource definitions:

| Object ID | Name | Key resource |
|-----------|------|--------------|
| 3303 | Temperature | 5700 (Sensor Value) |
| 3304 | Humidity | 5700 (Sensor Value) |
| 3305 | Power Measurement | 5700 (Sensor Value) |
| 3306 | Actuation | 5850 (On/Off) |
| 3308 | Set Point | 5900 (Set Point Value) |
| 3311 | Light Control | 5850 (On/Off), 5851 (Dimmer) |
| 3315 | Barometer | 5700 (Sensor Value) |
| 3316 | Voltage | 5700 (Sensor Value) |
| 3317 | Current | 5700 (Sensor Value) |
| 3318 | Frequency | 5700 (Sensor Value) |
| 3323 | Pressure | 5700 (Sensor Value) |
| 3325 | Concentration | 5700 (Sensor Value) |

Unknown object IDs are still accepted — they are treated as opaque float resources.

### Lightweight alternative (no runlet)

If your devices send to well-known LwM2M URIs using plain text payloads and you don't need registration, you can handle them with standard CoAP bindings instead of the full LwM2M runlet.
See the [how-to guide](how-to.md#receive-lwm2m-style-data-without-the-lwm2m-runlet).

### Runner setup

```tw
runner integrations from "Tinkwell.Runner.Headless.dll" {
    runlet lwm2m from "Tinkwell.Runlet.Lwm2m.dll";
}
```

The LwM2M runlet uses the [measures service](../measures-system.md) (discovered via service discovery) to write values.
Ensure a runner with the measures runlet is available.

---

## Error Handling

### Overview {#error-handling-overview}

By default, when a binding, action handler, or derived measure expression fails, Tinkwell **logs a warning and continues** (`resume next`).
This is the safest behavior — a single failure doesn't bring down the system.

The `on error` block gives you fine-grained control over what happens when something goes wrong.
You can configure error policies at multiple levels, add retry logic, and even publish failure events.

### The `on error` Block

The `on error` block is a special child block that can appear inside:

- **[Action blocks](#actions)** — default policy for all handlers in that action.
- **[Handler blocks (`do`)](#handler-blocks-do)** — policy for that specific handler (overrides the action default).
- **[Verb blocks (`on get`, `on post`, `on message`)](#verb-blocks-on)** — default policy for all bindings in that block.
- **[Binding blocks (`bind`)](#binding-blocks-bind)** — policy for that specific binding (overrides the verb default).
- **[Measure blocks](#derived-measures)** — policy for derived measure expression evaluation.

Syntax:

```tw
on error <policy> [retry N] [delay N] [backoff N];
```

Or with a body (only for the `publish` policy):

```tw
on error publish "event-name" [retry N] [delay N] [backoff N] {
    source = "my-source"
    name = "failure-detail"
}
```

### Error Policies

| Policy | Syntax | Behavior |
|--------|--------|----------|
| **Resume Next** | `on error resume next;` | Log a warning, skip the failed item, and continue with the next one. **This is the implicit default when no `on error` is specified.** |
| **Stop This** | `on error stop this;` | Log an error and permanently disable the failed handler/binding/measure for all future invocations. The rest of the system continues running. |
| **Stop Application** | `on error stop application;` | Log a critical error and shut down the entire application. Use for failures that cannot be recovered from. |
| **Publish** | `on error publish "event-name" { ... }` | Publish a failure event to the event bus and continue. Only available in actions (not CoAP/MQTT bindings or derived measures). |

### Retry Logic

Any policy can include retry modifiers.
Retries execute **before** the terminal policy is applied — the system attempts the operation multiple times, and only falls through to the policy if all attempts fail.

| Modifier | Required | Default | Description |
|----------|----------|---------|-------------|
| `retry N` | No | 0 (no retry) | Maximum number of retry attempts. Must be greater than 0 to enable retries. |
| `delay N` | No | `1000` | Base delay in milliseconds between retry attempts. |
| `backoff N` | No | `1.0` | Delay multiplier per attempt. `1` = fixed delay, `2` = exponential backoff. |

The delay between retries is calculated as:

```
delay = base_delay * backoff^(attempt - 1)
```

For example, `retry 3 delay 500 backoff 2` produces:
- Attempt 1 (initial): execute immediately.
- Attempt 2 (retry 1): wait 500ms.
- Attempt 3 (retry 2): wait 1000ms.
- Attempt 4 (retry 3): wait 2000ms.
- If all 4 attempts fail: apply the terminal policy.

With `retry 3 delay 1000` (no backoff specified, defaults to 1.0):
- Attempt 1: execute immediately.
- Attempts 2-4: wait 1000ms between each.

### The `publish` Policy

The `publish` policy is available **only in actions**.
It publishes a failure event with automatic error details and optional custom properties:

```tw
on error publish "handler-failure" retry 2 delay 1000 {
    source = "my-system"
    severity = "high"
}
```

The published event includes:

| Field | Value |
|-------|-------|
| `Source` | `"actions"` |
| `Verb` | `Failed` |
| `Name` | The event name you specified (e.g. `"handler-failure"`) |
| `Object` | The handler name that failed |
| Automatic payload | `_error_message`, `_error_type`, `_handler`, `_action`, `_event_source`, `_event_name` |
| Custom payload | Properties from the `on error` block body |

### Policy Inheritance

Error policies follow an inheritance model — a more specific policy overrides a more general one:

**In Actions:**

```tw
action alert when high-temp {
    on error resume next;           # Default for all handlers

    do log {
        message = (format("Alert: {Name}"))
        # Inherits "resume next" from the action
    }

    do mqtt-publish {
        topic = "alerts"
        payload = (format("{Name}"))
        on error stop this retry 3 delay 500 backoff 2;  # Overrides for this handler
    }
}
```

The `log` handler uses the action-level policy (`resume next`).
The `mqtt-publish` handler uses its own policy (`stop this` with 3 retries and exponential backoff).

**In CoAP/MQTT:**

```tw
coap sensors {
    resource "/sensor/+" {
        on post {
            on error resume next retry 2 delay 500;  # Default for all bindings

            bind measure {
                name = (segment(path, -1))
                on error stop this;                   # Override for this binding
            }

            bind event {
                source = coap
                verb = changed
                name = (segment(path, -1))
                # Inherits "resume next retry 2 delay 500" from the verb block
            }
        }
    }
}
```

### Error Handling in Actions

Actions support all four policies: `resume next`, `stop this`, `stop application`, and `publish`.

```tw
action critical-alert when system-failure {
    on error stop application;

    do log {
        message = (format("CRITICAL: {Name}"))
        level = critical
    }
    do mqtt-publish {
        topic = "alerts/critical"
        payload = (format("{Name}: {Object}"))
        on error publish "mqtt-failure" retry 5 delay 2000 backoff 1.5 {
            channel = ops
        }
    }
}
```

### Error Handling in CoAP Bindings

CoAP bindings support `resume next`, `stop this`, and `stop application`.
The `publish` policy is **not available** in CoAP bindings (it logs a warning and falls back to `resume next`).

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

### Error Handling in MQTT Bindings

MQTT bindings support `resume next`, `stop this`, and `stop application`.
The `publish` policy is **not available** in MQTT bindings (it logs a warning and falls back to `resume next`).

```tw
mqtt sensors {
    broker = "localhost"

    subscribe "sensor/+" {
        on message {
            on error resume next retry 3 delay 1000 backoff 2;

            bind event {
                source = mqtt
                verb = changed
                name = (segment(topic, -1))
                on error stop this;
            }

            bind measure {
                name = (segment(topic, -1))
                # Inherits verb-level policy with retry
            }
        }
    }
}
```

### Error Handling in Derived Measures

Derived measures support `resume next`, `stop this`, and `stop application`:

```tw
measure power {
    quantity = Power
    unit = Watt
    value = (voltage * current)
    on error resume next retry 2 delay 500;
}
```

Without an explicit `on error` block, the default is `resume next` — the failed evaluation cycle is skipped and the measure retains its last known value.

Retry is especially useful for derived measures whose dependencies may not be available at startup — the expression evaluation will be retried before the terminal policy kicks in.

```tw
measure derived-metric {
    value = (sensor_a + sensor_b)
    on error stop this;
}
```

With `stop this`, if the expression fails (e.g. a dependency doesn't exist), the measure is permanently disabled.

---

## Expressions

Expressions are evaluated at runtime using the NCalc engine.
They are used in derived measure values, signal conditions, `when` filters, handler parameters, and binding parameters.

A dedicated reference is available in the [Expressions Reference](expressions.md).
This section introduces the most commonly used features.

### Where Expressions Are Used

| Context | Written As | Variables Available | See |
|---------|-----------|---------------------|-----|
| [Derived measure](#derived-measures) `value` | `(expression)` or `@"expression"` | All other measure names | [Measures](#measures) |
| [Signal](#signals) `when`/`until` | `(expression)` | All measure names | [Signals](#signals) |
| [Signal](#signals) `for` | `(expression)` | All measure names | [Signals](#signals) |
| [Action](#actions) handler parameters | `(expression)` | Event fields (`Source`, `Verb`, `Name`, `Object`, `CorrelationId`, `Timestamp`) + payload entries | [Expression Variables in Actions](#expression-variables-in-actions) |
| [CoAP](#coap-integration) binding parameters | `(expression)` | `path`, `query`, `payload`, `method` | [Expression Variables in CoAP](#expression-variables-in-coap) |
| [MQTT](#mqtt-integration) binding parameters | `(expression)` | `topic`, `path`, `payload`, `method` | [Expression Variables in MQTT](#expression-variables-in-mqtt) |
| `when` filters | `(expression)` | Same as parent context | [When Filters](#when-filters) |
| Conditional `if` | `(expression)` | [`set` variables](#variables-and-interpolation) | [Conditional Blocks](#conditional-blocks) |

### Useful Functions

| Function | Description | Example |
|----------|-------------|---------|
| `segment(str, index)` | Splits a string by `/` and returns the segment at the given index. Negative indexes count from the end. | `segment('/sensor/temp', -1)` → `"temp"` |
| `json_value(str, path)` | Extracts a value from a JSON string using a dot-path or JSONPath. | `json_value(payload, 'temperature')` |
| `json_path(str, path)` | Navigates a JSON structure. | `json_path(payload, '$.devices[0].name')` |
| `make_json(k1, v1, ...)` | Builds a JSON string from key-value pairs. | `make_json('name', 'test', 'value', 42)` |
| `format(template)` | Replaces `{Placeholder}` tokens from the current variable context. | `format("Alert: {Name}")` |
| `if(cond, true_val, false_val)` | Conditional expression. | `if(temp > 80, 'hot', 'normal')` |
| `concat(a, b, ...)` | Concatenates strings. | `concat('sensor-', Name)` |
| `to_lower(str)` / `to_upper(str)` | Case conversion. | `to_lower(Name)` |
| `trim(str)` | Removes leading/trailing whitespace. | `trim(payload)` |
| `length(str)` | Returns the string length. | `length(payload)` |
| `starts_with(str, prefix)` | Tests if a string starts with a prefix. | `starts_with(topic, 'sensor/')` |
| `contains(str, substr)` | Tests if a string contains a substring. | `contains(payload, 'error')` |
| `replace(str, old, new)` | Replaces occurrences. | `replace(topic, '/', '.')` |
| `regex_match(str, pattern)` | Tests a regex match. | `regex_match(Name, '^temp.*')` |
| `regex_extract(str, pattern)` | Extracts the first regex match. | `regex_extract(payload, '\\d+')` |
| `now()` | Current UTC datetime. | `now()` |
| `sum(a, b, ...)` / `avg(...)` / `min(...)` / `max(...)` | Aggregate functions. | `avg(temp_a, temp_b, temp_c)` |
| `cint(val)` / `cdouble(val)` / `cstr(val)` | Type conversions. | `cdouble(payload)` |
| `base64_encode(str)` / `base64_decode(str)` | Base64 encoding/decoding. | `base64_encode(payload)` |

### The `quantity()` Function

Available in all expressions, `quantity()` converts between units using the UnitsNet library.
Unit abbreviations are listed in the [Units Reference](units.md):

```tw
# Convert 10 millivolts to the base unit (Volts) → 0.01
quantity(10, 'mV')

# Convert 10 millivolts to kilovolts → 0.00001
quantity(10, 'mV', 'kV')
```

This is useful in signal `for` durations and derived measure expressions:

```tw
signal alert when (temperature > quantity(176, '°F', '°C')) for (quantity(500, 'ms'));
```

---

## Complete Example

Here is a complete, realistic configuration file that demonstrates all major features — including [variables](#variables-and-interpolation), [templates](#templates), [conditional blocks](#conditional-blocks), [error handling with retry](#error-handling), and protocol bridging.

```tw
# =============================================================================
# Tinkwell Ensemble Configuration — Factory Floor Monitoring
# =============================================================================

# --- Variables (resolved at parse time, used with $"{{var}}") ---
set mqtt_broker = "mqtt-broker.local"
set mqtt_port = 1883
set coap_port = 5683
set plc_gateway = "plc-gateway.local"
set enable_mqtt = true
set storage_backend = memory

# --- Templates ---
template grpc-base {
    runlet store from "Tinkwell.Runlet.Store.dll" {
        storage = $"{{storage_backend}}"
    }
    runlet events from "Tinkwell.Runlet.Events.dll";
    @content
}

# --- Infrastructure ---
runner main from "Tinkwell.Runner.Grpc.dll" using grpc-base {
    runlet event-persistence from "Tinkwell.Runlet.EventPersistence.dll" {
        db-path = "events.db"
    }
    runlet measures from "Tinkwell.Runlet.Measures.dll";
    runlet signals from "Tinkwell.Runlet.Signals.dll";
    runlet measure-events from "Tinkwell.Runlet.MeasureEvents.dll";
}

runner background from "Tinkwell.Runner.Headless.dll" {
    runlet actions from "Tinkwell.Runlet.Actions.dll";
    runlet coap from "Tinkwell.Runlet.Coap.dll";
    runlet mqtt from "Tinkwell.Runlet.Mqtt.dll" if (enable_mqtt);
}

# --- Measures ---
measure voltage {
    quantity = "Electric Potential"
    unit = Volt
    minimum = 0
    maximum = 500
    precision = 2
    description = "Main bus voltage"
    category = electrical
}

measure current {
    quantity = "Electric Current"
    unit = Ampere
    minimum = 0
    maximum = 100
    precision = 2
    category = electrical
}

measure power {
    quantity = Power
    unit = Watt
    value = (voltage * current)
    precision = 1
    category = electrical
    on error resume next retry 2 delay 500;
}

measure ambient-temp {
    quantity = Temperature
    unit = DegreeCelsius
    minimum = -40
    maximum = 85
    precision = 1
    category = environment

    signal hot when (value > 50);
}

# --- Signals ---
signal high-power when (power > 5000) for "5 seconds" {
    severity = warning
    channel = ops
}

signal overheat when (ambient-temp > 70) until (ambient-temp < 60) {
    severity = critical
}

# --- CoAP ---
coap sensors {
    port = $"{{coap_port}}"

    resource "/measures/+" {
        on get {
            bind measure {
                name = (segment(path, -1))
            }
        }
        on post {
            on error resume next retry 2 delay 500;

            bind measure {
                name = (segment(path, -1))
            }
            bind event {
                source = coap
                verb = changed
                name = (segment(path, -1))
                object = (payload)
            }
            # Bridge: forward sensor data to the MQTT broker
            bind mqtt {
                topic = (format("sensors/{0}", segment(path, -1)))
                broker = $"{{mqtt_broker}}"
                port = $"{{mqtt_port}}"
                qos = 1
            }
        }
    }

    resource "/store/+" {
        on get {
            bind store {
                bucket = default
                key = (segment(path, -1))
            }
        }
        on post {
            bind store {
                bucket = default
                key = (segment(path, -1))
                ttl = 3600
            }
        }
    }
}

# --- MQTT ---
mqtt factory if (enable_mqtt) {
    broker = $"{{mqtt_broker}}"
    port = $"{{mqtt_port}}"

    subscribe "sensor/+" {
        on message {
            on error resume next retry 3 delay 1000 backoff 2;

            bind measure {
                name = (segment(topic, -1))
            }
            bind event {
                source = mqtt
                verb = changed
                name = (segment(topic, -1))
                object = (payload)
            }
        }
    }

    subscribe "device/+/telemetry" {
        on message {
            bind event {
                source = mqtt
                verb = changed
                name = (segment(topic, 1))
                object = (json_value(payload, 'value'))
                with payload {
                    unit = (json_value(payload, 'unit'))
                    device = (segment(topic, 1))
                }
            }
            # Bridge: forward device telemetry to the CoAP gateway
            bind coap {
                path = (format("/device/{0}", segment(topic, 1)))
                method = post
                host = $"{{plc_gateway}}"
            }
        }
    }
}

# --- Actions ---
action log-signals {
    source = signals
    verb = fired

    do log {
        message = (format("Signal {Name} fired (severity: {severity})"))
        level = warning
    }
}

action alert-high-power when high-power {
    on error resume next;

    do mqtt-publish {
        topic = (format("alerts/{Name}"))
        payload = (format("Power alert: {Object}W at {Timestamp}"))
        broker = $"{{mqtt_broker}}"
        on error stop this retry 3 delay 2000 backoff 1.5;
    }

    do coap-request {
        path = "/device/power-limit"
        method = post
        payload = "reduce"
        host = $"{{plc_gateway}}"
    }
}

action record-changes {
    verb = changed

    do update-entry {
        bucket = history
        key = (format("{Name}.{CorrelationId}"))
        value = (Object)
        ttl = 86400
    }
}
```

In this example:

- **`set` variables** (`mqtt_broker`, `plc_gateway`, etc.) define shared configuration values once at the top.
  If you move to a different broker or gateway, you only change one line.
- **`$"{{var}}"` interpolation** is used wherever those values are needed — in `broker`, `port`, and `host` properties across CoAP, MQTT, and action blocks.
  This is resolved at parse time (before runtime).
- **`template grpc-base`** defines the common store + events runlets and uses `@content` so each runner can add its own runlets.
- **`if (enable_mqtt)`** conditionally includes the MQTT runlet and connection block — set `enable_mqtt` to `false` to disable MQTT entirely.
- **`(format(...))` and `(segment(...))` expressions** are resolved at runtime against request/event data — notice how `$"..."` and `(...)` serve different purposes.
- **Protocol bridging** is shown in both directions: CoAP-to-MQTT (in the `on post` block) and MQTT-to-CoAP (in the `device/+/telemetry` subscription).
- **Error handling** is configured at multiple levels with retry and backoff.
