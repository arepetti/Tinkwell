# Derived Measures Configuration

This document describes the syntax for defining [derived measures](./Glossary.md#derived-measure) in Tinkwell's configuration files. These files, typically with a `.twm` extension, are used by the [Reducer](./Glossary.md#reducer) to calculate new measures from existing ones.

## Syntax Overview

A derived measure is defined using a `measure` block, which contains a set of key-value pairs. Lines starting with `//` are treated as comments and are ignored.

```tinkwell
// Import other measures
import "base_sensors.twm"

// Define a new derived measure
measure my_derived_measure {
    type: "ElectricalPower"
    unit: "Watt"
    expression: "voltage * current"
    description: "A brief explanation of the measure."
    minimum: 0
    maximum: 10000
    tags: "power, consumption"
}
```

## The `measure` block

Each `measure` block defines a new derived measure. It is identified by a unique name, which can be a simple identifier or a quoted string. To avoid unexpected behavior, the name should not contain special characters or reserved keywords like `let`, `when`, or `then`.

`measure <measure_name> { ... }`

-	`<measure_name>`: The unique identifier for the measure. Its dependencies must be defined or imported before it is used.

> **A Note on Naming**
>
> While names can be quoted strings to include spaces or dots, they must still adhere to certain rules.
>
> -   **Invalid Characters:** Names cannot contain `[`, `]`, `{`, `}`, `\`, `*`, `:`, `;`, `"`, `'`, `=`, `!`, or `?`.
> -   **Invalid Prefixes:** Names cannot start with `+`, `-`, `/`, or `__` (two underscores).
> -   **Discouraged Names:** To avoid conflicts, it is highly discouraged to use the following as names: `let`, `when`, `then`, `value`, `emit`.
>
> For simplicity, it is recommended to use [simple identifiers](./Glossary.md#simple-identifier). Simple identifiers do not need to be enclosed in double quotes. Furthermore, when used in an `expression`, they do not need to be enclosed in square brackets (`[]`).


### Attributes

Inside the `measure` block, you define the properties of the derived measure.

| Attribute     | Type      | Required | Description                                                                                                                             |
| :------------ | :-------- | :------- | :-------------------------------------------------------------------------------------------------------------------------------------- |
| `expression`  | String    | Yes      | A mathematical [expression](./Expressions.md) that calculates the measure's value. It can reference other measures by their name.         |
| `type`        | String    | No       | The quantity type (e.g., `Temperature`). Defaults to `Scalar`. See the [list of supported types](./Units.md).                           |
| `unit`        | String    | No       | The unit of measurement (e.g., `DegreeCelsius`). Must be valid for the specified `type`. See the [list of supported units](./Units.md). |
| `description` | String    | No       | A free-text description of the measure.                                                                                                 |
| `minimum`     | Number    | No       | The lower bound for the measure's value.                                                                                                |
| `maximum`     | Number    | No       | The upper bound for the measure's value.                                                                                                |
| `precision`   | Integer   | No       | The number of decimal places to round the value to.                                                                                     |
| `category`    | String    | No       | A classification label for grouping related measures.                                                                                   |
| `tags`        | String    | No       | A comma-separated list of keywords for organization and search.                                                                         |

### Value Types

All textual values must be enclosed in double quotes (`"`). For long strings, such as in `description` and `expression`, you can use a backslash (`\`) at the end of a line to continue the string on the next line:

```tinkwell
description: "This is a long description \
              that spans multiple lines."
```

## Signals

Signals are events emitted by the **Reducer** when certain conditions, based on the values of measures, are met. They are a powerful way to create alerts, trigger actions, or monitor the system's state.

A signal can be defined as a standalone entity or within the context of a specific `measure`.

### Syntax

A signal is defined using the `signal` keyword, followed by a name and a block of attributes.

```tinkwell
// Standalone signal
signal <signal_name> {
    when: "<condition>"
    [with: { <payload_properties> }]
}

// Signal within a measure
measure my_measure {
    // ...
    signal <signal_name> {
        when: "<condition>"
        [with: { <payload_properties> }]
    }
}
```

### Attributes

| Attribute | Type      | Required | Description                                                                                             |
| :-------- | :-------- | :------- | :------------------------------------------------------------------------------------------------------ |
| `when`    | String    | Yes      | An [expression](./Expressions.md) that must evaluate to `true` for the signal to be emitted.            |
| `with`    | Dictionary| No       | An optional block of key-value pairs that will be added to the payload of the emitted event.            |

When a signal is defined inside a measure, the `when` condition can directly reference the measure's value by its name (e.g., `when: "value > 100"`).

## Imports

You can organize your measures by splitting them into multiple files and using the `import` directive to include them. The path is relative to the current file.

`import "common_measures.twm"`

## Complete Example

```tinkwell
// File: /config/derived_measures.twm

// Import base measures from another file
import "base_sensors.twm"

// Calculate electrical power and define signals for it
measure electrical_power {
    type: "ElectricalPower"
    unit: "Watt"
    expression: "voltage * current"
    description: "Calculates electrical power from voltage and current."
    minimum: 0.0
    maximum: 5000.0
    tags: "electrical, power, energy"
    category: "Energy Management"

    // Signal for high power consumption
    signal high_load {
      when: "value > 4500" // `value` refers to electrical_power
      with {
        severity: "critical"
      }
    }

    // Signal for low power consumption
    signal low_load {
      when: "value < 100"
    }
}

// Define a standalone signal for a general system alert
signal low_battery {
  when: "system_voltage < 24 and system_current < 10"
  with {
    subject: main_battery,
    severity: "warning"
  }
}
```