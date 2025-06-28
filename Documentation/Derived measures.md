# Tinkwell Reducer Configuration Syntax

This document describes the syntax used to define **derived measures** in Tinkwell Reducer's configuration files.

---

## Syntax Overview

Each configuration file consists of one or more `measure` blocks:

```ebnf
config           ::= (measure_block)*
measure_block    ::= 'measure' QUOTED_IDENTIFIER '{' measure_property* '}'
measure_property ::= property_key ':' property_value
property_key     ::= 'type' | 'unit' | 'expression' | 'description' | 'minimum'
                   | 'maximum' | 'tags' | 'category' | 'precision'
property_value   ::= QUOTED_STRING | number_literal | identifier_list
```

---

### 📝 String Values

All textual values in the configuration must be wrapped in **double quotes** (`"`). These include fields like `type`, `unit`, `expression`, `description`, and `category`.

#### 🔹 Basic Strings

Basic quoted string values look like this:

```text
unit: "Watt"
type: "ElectricalPower"
category: "Energy Monitoring"
```

#### 🔹 Escaping Characters

To include special characters like double quotes or backslashes inside a string, use escape sequences:

| Character     | Escape Syntax | Example                                   |
|---------------|----------------|------------------------------------------|
| Double quote  | `\"`           | `"Voltage is measured in \"Volts\""`     |
| Backslash     | `\\`           | `"Path is C:\\Program Files\\App"`       |
| Newline       | `\n`           | `"First line.\nSecond line."`            |
| Tab           | `\t`           | `"Column A:\tValue"`                     |

These follow **C-style** string escape rules and are parsed accordingly.

#### 🔹 Multiline Strings

Use a backslash (`\`) at the end of the line to indicate that the string continues on the next line. This works for values like `description` and `expression`:

```text
description: "Calculates power consumption. \
              Useful in energy audits."

expression: "[Zone1] + [Zone2] + \
             [Zone3]"
```

Each backslash should be placed **at the very end of the line** with no trailing characters after it. The continuation line may be indented for readability.

---

## Basic Structure

```text
measure "Measure.Name" {
    key: value
    ...
}
```

The measure name must be enclosed in double quotes. The name of the measure can be any alphanumeric value (plus `-_.` and spaces) Keys and values are separated by `:`, no quotes are needed for most values. Lines starting with `//` are considered comments and ignored.

---

## Reference

### `type` *(optional)*

Specifies the quantity type:

```text
type: Electrical Power
```
It must be a valid _quantity type_ identifier. For legibility the single word can be separate with spaces, these two are then equivalent:

```text
 measure "Power 1" {
    type: Electrical Power
    unit: Watt
    expression: Voltage1 * Current1
}

 measure "Power 2" {
    type: ElectricalPower
    unit: Watt
    expression: Voltage2 * Current2
}
```

Default: `Scalar`

---

### `unit` *(optional)*

Defines the unit of measurement.

```text
unit: Watt
```

As for `type` the words can be separated with spaces. 

Default: empty string

---

### `expression` *(required)*

A mathematical expression. You can use any measure in the system, they must be already defined when this configuration file is parsed.
If the name of a measure is alphanumeric (and it does not start with a number) then you do not need to enclose it with square brackets.

```text
expression: Voltage * Current
```

If the expression is long you can split it over multiple lines with `\`:

```text
expression: [Zone1.Temperature] + \
            [Zone2.Temperature] + \
            [Zone3.Temperature]
```

---

### `description` *(optional)*

Free text. Supports multi-line continuation:

```text
description: Calculates total energy. \
             Used for dashboard summaries.
```

---

### `minimum`, `maximum` *(optional)*

Numeric boundaries for the derived value:

```text
minimum: 0.0
maximum: 10000.0
```
You can specify only `minimum` or only `maximum`. When a boundaries is specified then attempting to store a value outside the allowed range will cause a run-time error.

---

### `tags` *(optional)*

Comma-separated list of keywords:

```text
tags: energy, power, analytics
```

You can use this to group your measures and searches (using `List()` from the `Tinkwell.Store` service) support filtering by tag.

---

### `category` *(optional)*

A classification label:

```text
category: Energy Management
```

You can categorize your measures and searches (using `List()` from `Tinkwell.Store` service) support filtering by category.

---

### `precision` *(optional)*

Specifies how many decimal places to round to:

```text
precision: 2
```

Note that this rounding is not for presentation purposes, if specified then the value stored has this number of decimals. If omitted then the number is not rounded and it's stored in double precision.

---

## Full Example

```text
// This is a new derived measure, "Electrical.Power" is its name
 measure "Electrical.Power" {
    type: Electrical Power
    unit: Watt
    expression: Voltage * Current
    description: Calculates the electrical power from voltage and current. \
                This measure is fundamental for energy monitoring.
    minimum: 0.0
    maximum: 5000.0
    tags: electrical, power, energy, calculation
    category: Energy Management
    precision: 2
}

 measure "HVAC.AverageTemperature" {
    type: Temperature
    unit: Degree Celsius
    expression: ([Zone1.Temperature] + [Zone2.Temperature] + [Zone3.Temperature]) / 3
    description: Computes the average temperature across three HVAC zones. \
                Useful for overall building climate control.
    minimum: 18.0
    maximum: 25.0
    tags: hvac, temperature, average, climate
    category: Building Automation
    precision: 1 
}

 measure "System.UptimeHours" {
    type: Duration
    unit: Hour
    expression: [System.UptimeSeconds] / 3600
    description: Converts system uptime from seconds to hours. \
                Provides a more readable format for long-term operation.
    tags: system, uptime, monitoring
    category: IT Infrastructure
    precision: 0
}
```
