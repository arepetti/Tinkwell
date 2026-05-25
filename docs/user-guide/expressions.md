# Expressions Reference

Tinkwell expressions are evaluated at runtime by the [NCalc](https://github.com/ncalc/ncalc) engine, extended with Tinkwell-specific functions.
This page is the complete reference for syntax, operators, and all available functions.

---

## Syntax

### Writing expressions

Expressions appear in `.tw` files in two forms:

```tw
value = (voltage * current)       # parenthesized
value = @"(a + b) / 2"           # verbatim string
```

Both produce the same result.
Use `@"..."` when the expression contains characters that would be awkward inside parentheses.

Inside parenthesized expressions, use **single quotes** for string literals:

```tw
name = (segment(path, -1))
object = (json_value(payload, '$.temperature'))
status = (if(temp > 80, 'critical', 'normal'))
```

### Parameters

Expressions can reference named parameters from their context.
Parameter names are **case-sensitive**:

```tw
# 'voltage' and 'current' are measure names available as parameters
value = (voltage * current)
```

If a parameter name contains characters that aren't valid in a simple identifier (letters, digits, underscore), enclose it in square brackets:

```tw
value = ([ambient-temp] * 1.5)
signal hot when ([floor1.temperature] > 80);
```

This is necessary for Tinkwell measure names that use hyphens or dots, since NCalc would otherwise parse `ambient-temp` as `ambient` minus `temp`.

Parameters that don't exist evaluate to `null`.

### Evaluation timeout

Every expression evaluation has a **5-second timeout** by default.
If an expression takes longer, it throws an error.

---

## Operators

### Arithmetic

| Operator | Description | Example |
|----------|-------------|---------|
| `+` | Addition (or string concatenation) | `a + b` |
| `-` | Subtraction (or unary negation) | `a - b`, `-a` |
| `*` | Multiplication | `a * b` |
| `/` | Division | `a / b` |
| `%` | Modulo | `a % b` |

### Comparison

| Operator | Description |
|----------|-------------|
| `=`, `==` | Equal |
| `!=`, `<>` | Not equal |
| `<` | Less than |
| `<=` | Less than or equal |
| `>` | Greater than |
| `>=` | Greater than or equal |

### Logical

| Operator | Description |
|----------|-------------|
| `and`, `&&` | Logical AND |
| `or`, `\|\|` | Logical OR |
| `not`, `!` | Logical NOT |

### Bitwise

| Operator | Description |
|----------|-------------|
| `&` | Bitwise AND |
| `\|` | Bitwise OR |
| `^` | Bitwise XOR |
| `<<` | Left shift |
| `>>` | Right shift |

### Precedence

Standard mathematical precedence applies.
Use parentheses to override:

```tw
value = ((a + b) * c)
```

---

## NCalc Built-In Functions

These are provided by the NCalc engine.
Function names are **case-insensitive**.

### Math

| Function | Description | Example |
|----------|-------------|---------|
| `abs(x)` | Absolute value | `abs(-5)` → `5` |
| `ceiling(x)` | Round up | `ceiling(4.1)` → `5` |
| `floor(x)` | Round down | `floor(4.9)` → `4` |
| `round(x, d)` | Round to `d` decimals | `round(3.456, 2)` → `3.46` |
| `truncate(x)` | Remove fractional part | `truncate(4.9)` → `4` |
| `sign(x)` | Sign (-1, 0, or 1) | `sign(-5)` → `-1` |
| `max(a, b)` | Larger of two values | `max(3, 7)` → `7` |
| `min(a, b)` | Smaller of two values | `min(3, 7)` → `3` |
| `pow(x, y)` | Power | `pow(2, 10)` → `1024` |
| `sqrt(x)` | Square root | `sqrt(9)` → `3` |
| `exp(x)` | e^x | `exp(1)` → `2.718...` |
| `log(x)` | Natural logarithm | `log(exp(1))` → `1` |
| `log10(x)` | Base-10 logarithm | `log10(100)` → `2` |

### Trigonometry

| Function | Description |
|----------|-------------|
| `cos(x)`, `sin(x)`, `tan(x)` | Trig functions (radians) |
| `acos(x)`, `asin(x)`, `atan(x)` | Inverse trig functions |

### Conditional

```tw
if(condition, value_if_true, value_if_false)
```

```tw
value = (if(temperature > 80, 'critical', if(temperature > 60, 'warning', 'normal')))
```

### Membership

```tw
value in (1, 2, 3)
name in ('alpha', 'beta')
```

---

## Tinkwell Functions

All Tinkwell function names use **snake_case** and are **case-sensitive**.

### String Functions

| Function | Parameters | Returns | Description |
|----------|-----------|---------|-------------|
| `is_null(x)` | any | bool | True if `x` is null. |
| `is_null_or_empty(s)` | string | bool | True if null or `""`. |
| `is_null_or_white_space(s)` | string | bool | True if null, empty, or whitespace only. |
| `has_value(x)` | any | bool | False for null or whitespace-only strings; true otherwise. |
| `length(s)` | string? | int | String length. `null` → `0`. |
| `or_empty(s)` | string? | string | Returns `s` or `""` if null. |
| `trim(s)` | string? | string | Removes leading/trailing whitespace. |
| `to_lower(s)` | string? | string? | Lowercase (invariant). |
| `to_upper(s)` | string? | string? | Uppercase (invariant). |
| `concat(a, b)` | string?, string? | string | Concatenates two strings. |
| `substring(s, start, len)` | string, int, int | string | Extracts a substring. Length is clamped to available characters. |
| `replace(s, old, new)` | string, string, string | string | Replaces all occurrences (case-insensitive). |
| `split(s, sep)` | string, string | string[] | Splits by separator. |
| `join(sep, items)` | string, IEnumerable | string | Joins items with separator. |
| `starts_with(s, prefix)` | string, string | bool | Case-insensitive prefix test. |
| `ends_with(s, suffix)` | string, string | bool | Case-insensitive suffix test. |
| `contains(s, sub)` | string, string | bool | Case-insensitive substring test. |
| `regex_match(s, pattern)` | string, string | bool | Tests a regex pattern. |
| `regex_extract(s, pattern, group)` | string, string, int | string? | Returns the specified capture group, or null if no match. |

### Path and Segment Functions

| Function | Parameters | Returns | Description |
|----------|-----------|---------|-------------|
| `segment(s, index)` | string, int | string | Splits by `/` and returns the segment at `index`. Negative indexes count from the end (`-1` = last). |
| `segment_at(s, sep, index)` | string, string, int | string | Same as `segment` but with a custom separator. |

```tw
segment('/sensors/floor1/temp', -1)       # → "temp"
segment('/sensors/floor1/temp', 1)        # → "sensors"
segment_at('a.b.c', '.', 0)              # → "a"
```

Out-of-range indexes throw an error.

### JSON Functions

| Function | Parameters | Returns | Description |
|----------|-----------|---------|-------------|
| `json_value(json, path)` | string, string | any | Extracts a scalar value from JSON using a dot-path. Returns string, number, bool, or null. |
| `json_path(json, path)` | string, string | JsonElement | Navigates a JSON structure. Throws on invalid path. |
| `make_json(k1, v1, ...)` | even number of args | string | Builds a JSON object from key-value pairs. |
| `json_encode(s)` | string? | string | JSON-encodes a string (adds quotes, escapes). |

```tw
json_value(payload, 'temperature')        # dot-path
json_value(payload, '$.devices[0].name')  # JSONPath
make_json('name', 'test', 'value', 42)    # → {"name":"test","value":42}
```

`make_json` requires an **even** number of arguments (key-value pairs).
Odd count throws an error.

### Template Function

| Function | Parameters | Returns | Description |
|----------|-----------|---------|-------------|
| `format(template)` | string | string | Replaces `{Placeholder}` tokens with values from the current expression context. |

```tw
# In an action where Source, Name, Object are event fields:
format("Alert: {Name} from {Source}")

# Unknown placeholders are left as-is:
format("Value: {Unknown}")  # → "Value: {Unknown}"

# Null values become empty strings:
format("Result: {MaybeNull}")  # → "Result: "
```

`format()` operates at **runtime** against expression parameters.
This is different from `$"{{var}}"` interpolation which runs at **parse time** against `set` variables.

### Type Conversion Functions

Names are `cint`, `cstr`, etc. (no underscore after the leading `c`): the default `ExpressionFunction` name mapping turns `CInt` into `cint` because the regex only inserts an underscore *after* a lowercase/digit, not after the initial `C`.

| Function | Parameters | Returns | Description |
|----------|-----------|---------|-------------|
| `cint(x)` | any | int | Converts to integer. |
| `clong(x)` | any | long | Converts to long. |
| `cfloat(x)` | any | float | Converts to float. |
| `cdouble(x)` | any | double | Converts to double. |
| `cstr(x)` | any | string | Converts to string. |
| `cbool(x)` | any | bool | Converts to boolean. |

`cbool` conversion rules:
- `null` → `false`
- `true`/`false` → as-is
- Strings: `"true"`, `"yes"`, `"on"` → `true`; `"false"`, `"no"`, `"off"` → `false`
- Numbers: non-zero → `true`, zero → `false`
- Other types → `true`

Any other string value (e.g. `"maybe"`) throws an error.

### Date and Time Functions

| Function | Parameters | Returns | Description |
|----------|-----------|---------|-------------|
| `now()` | — | DateTime | Current UTC time. |
| `parse_date(s)` | string | DateTime | Parses a date string (invariant culture, adjusted to UTC). |
| `format_date(dt, fmt)` | DateTime, string | string | Formats a date using a .NET format string. |
| `date_diff(a, b)` | DateTime, DateTime | TimeSpan | `a - b`. |
| `date_add(dt, ts)` | DateTime, TimeSpan | DateTime | Adds a timespan to a date. |
| `year(dt)` | DateTime | int | Extracts the year. |
| `month(dt)` | DateTime | int | Extracts the month. |
| `day(dt)` | DateTime | int | Extracts the day. |
| `hour(dt)` | DateTime | int | Extracts the hour. |
| `minute(dt)` | DateTime | int | Extracts the minute. |
| `second(dt)` | DateTime | int | Extracts the second. |
| `parse_timespan(s)` | string | TimeSpan | Parses a duration: `"5d"`, `"10h"`, `"30m"`, `"15s"`, or standard .NET format. |
| `timespan_add(a, b)` | TimeSpan, TimeSpan | TimeSpan | Adds two timespans. |
| `timespan_diff(a, b)` | TimeSpan, TimeSpan | TimeSpan | `a - b`. |
| `ago(ts)` | TimeSpan | DateTime | `UtcNow - ts`. |
| `from_now(ts)` | TimeSpan | DateTime | `UtcNow + ts`. |
| `time(s)` | string | double | Converts a time string (`HH:mm` or `HH:mm:ss`, 24-hour) to seconds since midnight for time-of-day comparisons with the `wallclock` measure. |

### Collection Functions

| Function | Parameters | Returns | Description |
|----------|-----------|---------|-------------|
| `count(items)` | IEnumerable? | int | Number of items. `null` → `0`. |
| `at(items, index)` | IEnumerable?, int | any | Item at index. `null` → default. |
| `first(items)` | IEnumerable? | any | First item. `null` → default. |
| `last(items)` | IEnumerable? | any | Last item. `null` → default. |
| `skip(items, n)` | IEnumerable?, int | IEnumerable | Skips first `n` items. |
| `take(items, n)` | IEnumerable?, int | IEnumerable | Takes first `n` items. |
| `sum(items)` | IEnumerable | double | Sum. Throws on null or empty. |
| `avg(items)` | IEnumerable | double | Average. Throws on null or empty. |
| `min(items)` | IEnumerable | double | Minimum. Throws on null or empty. |
| `max(items)` | IEnumerable | double | Maximum. Throws on null or empty. |

### Encoding and Hashing Functions

| Function | Parameters | Returns | Description |
|----------|-----------|---------|-------------|
| `base64_encode(s)` | string | string | Base64-encodes a UTF-8 string. |
| `base64_decode(s)` | string | string | Decodes a Base64 string to UTF-8. |
| `url_encode(s)` | string? | string | URL-encodes a string. |
| `url_decode(s)` | string? | string | URL-decodes a string. |
| `md5(s)` | string | string | MD5 hash (lowercase hex). |
| `sha256(s)` | string | string | SHA-256 hash (lowercase hex). |
| `sha512(s)` | string | string | SHA-512 hash (lowercase hex). |

### Unit Conversion

| Function | Parameters | Returns | Description |
|----------|-----------|---------|-------------|
| `quantity(value, unit)` | number, string | double | Converts to the SI base unit of that quantity type. |
| `quantity(value, from, to)` | number, string, string | double | Converts between two explicit units. |

Unit strings use [UnitsNet abbreviations](units.md).
See the [Units Reference](units.md) for the full list.

```tw
quantity(10, 'mV')            # → 0.01 (Volts, the base unit)
quantity(10, 'mV', 'kV')      # → 0.00001
quantity(100, '°C', '°F')     # → 212
quantity(1.5, 'm', 'mm')      # → 1500
```

Throws an error on unknown units, incompatible unit types, or wrong argument count.

Common use in signal durations:

```tw
signal alert when (temp > 80) for (quantity(500, 'ms'));
```

---

## Boolean Coercion

When an expression result is used as a boolean (e.g. `when`, `until`, `if` conditions), these rules apply:

| Value | Result |
|-------|--------|
| `null` | `false` |
| `true` / `false` | as-is |
| `"true"`, `"yes"`, `"on"` | `true` |
| `"false"`, `"no"`, `"off"` | `false` |
| Non-zero number | `true` |
| Zero | `false` |
| Any other string | error |

---

## Expression Contexts

Each context provides different parameters.
The expression itself is the same NCalc language everywhere — only the available variables differ.

### Derived measures

Parameters: all other measure names (by name, current numeric value).

```tw
measure power {
    value = (voltage * current)
}
```

Circular dependencies are detected and rejected at startup.
If a referenced measure doesn't exist yet, the expression fails and the [error policy](configuration.md#error-handling-in-derived-measures) applies.

### Signal `when` / `until`

Parameters: all measure names.

```tw
signal overheat when (temp > 80) until (temp < 70);
signal combined when (temperature > 80 or pressure > 200);
```

Inside a measure block, `value` refers to the enclosing measure:

```tw
measure temperature {
    signal hot when (value > 50);  # equivalent to (temperature > 50)
}
```

### Signal `for`

Parameters: all measure names.
The result must be numeric and is interpreted as **seconds**.

```tw
for 10                         # literal: 10 seconds
for "5 seconds"                # parsed by UnitsNet
for (cycle_time / 10)          # expression, result in seconds
for (quantity(500, 'ms'))      # 0.5 seconds via unit conversion
```

### Action handler parameters

Parameters: the triggering event's fields.

| Parameter | Type | Description |
|-----------|------|-------------|
| `Source` | string | Event source (e.g. `"signals"`, `"measures"`) |
| `Verb` | string | Event verb (lowercase) |
| `Name` | string | Event name |
| `Object` | string? | Event object/value |
| `CorrelationId` | string? | Correlation ID |
| `Timestamp` | DateTime | Event timestamp |
| Payload entries | varies | Additional event payload key-value pairs |

```tw
do log {
    message = (format("Signal {Name} fired from {Source}"))
}
```

### CoAP binding parameters

| Parameter | Description |
|-----------|-------------|
| `path` | URI path of the request |
| `query` | Query string |
| `payload` | Request body (string) |
| `method` | `"GET"`, `"POST"`, `"PUT"`, or `"DELETE"` |
| `peer_ip` | IP address of the remote sender (e.g. `"192.168.1.42"`) — empty string if unknown |
| `peer_identity` | DTLS PSK identity or certificate CN — empty string until DTLS is implemented |

```tw
bind measure {
    name = (segment(path, -1))
}

on post when (json_value(payload, '$.severity') == 'critical') {
    bind event { ... }
}
```

### MQTT binding parameters

| Parameter | Description |
|-----------|-------------|
| `topic` | Full MQTT topic |
| `path` | Alias for `topic` |
| `payload` | Message payload (string) |
| `method` | Always `"MESSAGE"` |

```tw
bind measure {
    name = (segment(topic, -1))
    value = (json_value(payload, '$.value'))
}
```

### Conditional `if` blocks (parse-time)

Parameters: variables defined with `set` and model properties.
Evaluated at **parse time**, not runtime.

```tw
set enable_mqtt = true

runner integrations from "Tinkwell.Runner.Headless.dll" if (enable_mqtt) {
    runlet mqtt from "Tinkwell.Runlet.Mqtt.dll";
}
```

---

## Error Handling

When an expression fails at runtime:

- **Derived measures**: the [error policy](configuration.md#error-handling-in-derived-measures) applies (`resume next` by default — the measure keeps its last value).
- **Signal `when`**: a failed evaluation is treated as `false` (the signal does not fire).
  A warning is logged.
- **Signal `for`**: a failed evaluation crashes the signal worker.
- **Bindings**: the binding's [error policy](configuration.md#error-handling) applies.
- **Actions**: the action's error policy applies.

Type coercion errors (e.g. passing a string where a number is expected) and unknown function names produce `ExpressionEvaluationException`.
