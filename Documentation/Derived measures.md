# Derived Measures Configuration

This document describes the syntax for defining **derived measures** in Tinkwell's configuration files. These files, typically with a `.twm` extension, are used by the [Reducer](#reducer) to calculate new measures from existing ones.

## Syntax Overview

A derived measure is defined using a `measure` block, which contains a set of key-value pairs.

### Basic Structure

```tinkwell
// Forward-declare measures defined elsewhere
import "path/to/another_file.twm"

// Define a new derived measure
measure "Measure.Name" {
    key: value
    ...
}
```

-   **`import`**: The `import` directive, which must appear at the top of the file, allows you to reference measures defined in other files. This is useful for organizing complex configurations.
-   **`measure`**: Each `measure` block defines a new derived measure. The name can be a [simple identifier](./Glossary.md#simple-identifier) or a string enclosed in double quotes.
-   **Comments**: Lines starting with `//` are treated as comments and are ignored.

### String Values

All textual values, such as `type`, `unit`, and `description`, must be enclosed in double quotes (`"`).

#### Multiline Strings

For long strings, like `description` and `expression`, you can use a backslash (`\`) at the end of a line to continue the string on the next line:

```tinkwell
description: "This is a long description \
              that spans multiple lines."

expression: "[Zone1.Temperature] + \
             [Zone2.Temperature]"
```

## Attributes Reference

### `name` (required)

The unique identifier for the measure. It can be a [simple identifier](./Glossary.md#simple-identifier) or a quoted string.

```tinkwell
measure "Electrical.Power" { ... }
```

### `expression` (required)

A mathematical [expression](./Expressions.md) that calculates the value of the derived measure. It can reference other measures, which must be defined or imported before use.

```tinkwell
expression: "[Voltage] * [Current]"
```

### `type` (optional)

Specifies the quantity type (e.g., `ElectricalPower`, `Temperature`). If omitted, it defaults to `Scalar`.

```tinkwell
type: "ElectricalPower"
```

### `unit` (optional)

Defines the unit of measurement (e.g., `Watt`, `DegreeCelsius`). It must be a valid unit for the specified `type`. Defaults to an empty string.

```tinkwell
unit: "Watt"
```

### `description` (optional)

A free-text description of the measure. Supports multiline strings.

```tinkwell
description: "Calculates the total power consumption."
```

### `minimum` and `maximum` (optional)

Numeric boundaries for the measure's value. If a calculated value falls outside this range, a runtime error will occur.

```tinkwell
minimum: 0.0
maximum: 10000.0
```

### `tags` (optional)

A comma-separated list of keywords for organizing and searching for measures.

```tinkwell
tags: "energy, power, analytics"
```

### `category` (optional)

A classification label for grouping related measures.

```tinkwell
category: "Energy Management"
```

### `precision` (optional)

Specifies the number of decimal places to round the calculated value to. This affects the stored value, not just its presentation.

```tinkwell
precision: 2
```

## Complete Example

```tinkwell
// File: /config/derived_measures.twm

// Import base measures from another file
import "./base_sensors.twm"

// Calculate electrical power
measure "Electrical.Power" {
    type: "ElectricalPower"
    unit: "Watt"
    expression: "[Voltage] * [Current]"
    description: "Calculates electrical power from voltage and current. \
                This is fundamental for energy monitoring."
    minimum: 0.0
    maximum: 5000.0
    tags: "electrical, power, energy, calculation"
    category: "Energy Management"
    precision: 2
}

// Calculate average temperature
measure "HVAC.AverageTemperature" {
    type: "Temperature"
    unit: "DegreeCelsius"
    expression: "([Zone1.Temperature] + [Zone2.Temperature] + [Zone3.Temperature]) / 3"
    description: "Computes the average temperature across three HVAC zones. \
                Useful for overall building climate control."
    minimum: 18.0
    maximum: 25.0
    tags: "hvac, temperature, average, climate"
    category: "Building Automation"
    precision: 1
}

// Convert uptime to a more readable format
measure "System.UptimeHours" {
    type: "Duration"
    unit: "Hour"
    expression: "[System.UptimeSeconds] / 3600"
    description: "Converts system uptime from seconds to hours."
    tags: "system, uptime, monitoring"
    category: "IT Infrastructure"
    precision: 0
}
```