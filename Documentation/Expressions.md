# Expressions

Tinkwell uses a powerful expression syntax to allow for dynamic configuration and logic. This allows you to define conditional runners, derived measures, and reactive rules.

## Operator Precedence

The operators are listed in order from highest to lowest precedence:

1.  Primary: `()`
2.  Unary: `!`, `not`, `~`
3.  Multiplicative: `*`, `/`, `%`
4.  Additive: `+`, `-`
5.  Shift: `<<`, `>>`
6.  Relational: `<`, `>`, `<=`, `>=`
7.  Equality: `==`, `!=`
8.  Bitwise AND: `&`
9.  Bitwise XOR: `^`
10. Bitwise OR: `|`
11. Logical AND: `&&`, `and`
12. Logical OR: `||`, `or`

## Literals

-   Numbers: `42`, `3.14`
-   Strings: `'hello'`
-   Booleans: `true`, `false`
-   Dates: `#2025-01-01#`

## Parameters

You can reference parameters by name. If they are [simple identifiers](./Glossary.md#simple-identifier), you can write them directly:

```text
voltage * current
```

Otherwise, you must enclose them in square brackets:

```text
[main voltage] * [input current]
```

Some predefined parameters may be available in specific contexts.

## Functions

The expression engine supports a rich set of built-in and custom functions.

- [General Purpose](#general-purpose)
- [Math](#math)
- [Date & Time](#date--time)
- [String Manipulation](#string-manipulation)
- [Collection Handling](#collection-handling)
- [Type Conversion](#type-conversion)
- [JSON Parsing](#json-parsing)
- [Security](#security)

### General Purpose

| Function | Description |
| :--- | :--- |
| `if(condition: boolean, true_value, false_value)` | Returns `true_value` if `condition` is true, otherwise `false_value`. See [Detailed Examples](#if-function). |
| `in(value, item1, item2, ...items): boolean` | Checks if `value` is present in the provided list of items. |
| `is_null(value): boolean` | Returns `true` if the value is `null`. |
| `is_null_or_empty(text: string): boolean` | Returns `true` if the given string is `null` or an empty string (`""`). |
| `is_null_or_white_space(text: string): boolean` | Returns `true` if the given string is `null`, empty (`""`), or consists only of white-space characters. |

<a name="if-function"></a>
#### `if(condition: boolean, true_value, false_value)`
Returns `true_value` if `condition` is true, otherwise `false_value`.

**Examples:**
```tinkwell
if(temperature > 100, "High", "Normal") // Returns "High" if temperature > 100, else "Normal"
if(is_null(sensor_data), "No data", sensor_data.value) // Handles null sensor data
```

[Back to index](#functions)

### Math

| Function | Description |
| :--- | :--- |
| `abs(x: number): number` | Returns the absolute value of a number. |
| `acos(x: number): number` | Returns the angle whose cosine is the specified number. The result is in radians. |
| `asin(x: number): number` | Returns the angle whose sine is the specified number. The result is in radians. |
| `atan(x: number): number` | Returns the angle whose tangent is the specified number. The result is in radians. |
| `ceiling(x: number): number` | Returns the smallest integer greater than or equal to the specified number. |
| `cos(x: number): number` | Returns the cosine of the specified angle (in radians). |
| `exp(x: number): number` | Returns `e` raised to the power of the specified number. |
| `floor(x: number): number` | Returns the largest integer less than or equal to the specified number. |
| `ieee_remainder(x: number, y: number): number` | Returns the remainder resulting from the division of `x` by `y` as specified by the IEEE 754 standard. |
| `log(x: number): number` | Returns the natural (base `e`) logarithm of a specified number. |
| `log10(x: number): number` | Returns the base 10 logarithm of a specified number. |
| `max(x: number, y: number): number` | Returns the larger of two numbers. |
| `min(x: number, y: number): number` | Returns the smaller of two numbers. |
| `pow(x: number, y: number): number` | Returns `x` raised to the power of `y`. |
| `round(x: number): number` | Rounds a number to the nearest integer. Midpoint values are rounded to the nearest even number (e.g., 2.5 rounds to 2, 3.5 rounds to 4). |
| `sign(x: number): number` | Returns a value indicating the sign of a number: -1 if negative, 0 if zero, 1 if positive. |
| `sin(x: number): number` | Returns the sine of the specified angle (in radians). |
| `sqrt(x: number): number` | Returns the square root of a specified number. |
| `tan(x: number): number` | Returns the tangent of the specified angle (in radians). |
| `truncate(x: number): number` | Calculates the integer part of a specified number by discarding any fractional digits. |

[Back to index](#functions)

### Date & Time

| Function | Description |
| :--- | :--- |
| `now(): DateTime` | Returns the current UTC date and time. |
| `parse_date(text: string): DateTime` | Parses a string into a `DateTime` object. The string must be in a valid date/time format (e.g., ISO 8601). |
| `format_date(date: DateTime, format: string): string` | Formats a `DateTime` object into a string using a specified format. The format string uses standard .NET custom date and time format specifiers. |
| `date_diff(d1: DateTime, d2: DateTime): TimeSpan` | Calculates the duration (`TimeSpan`) between two `DateTime` objects (`d1 - d2`). |
| `date_add(date: DateTime, timespan: TimeSpan): DateTime` | Adds a `TimeSpan` to a `DateTime` object. |
| `year(date: DateTime): number` | Extracts the year component from a `DateTime` object. |
| `month(date: DateTime): number` | Extracts the month component (1-12) from a `DateTime` object. |
| `day(date: DateTime): number` | Extracts the day of the month component (1-31) from a `DateTime` object. |
| `hour(date: DateTime): number` | Extracts the hour component (0-23) from a `DateTime` object. |
| `minute(date: DateTime): number` | Extracts the minute component (0-59) from a `DateTime` object. |
| `second(date: DateTime): number` | Extracts the second component (0-59) from a `DateTime` object. |
| `parse_timespan(text: string): TimeSpan` | Creates a `TimeSpan` object from a string. Supports standard `d.hh:mm:ss` format and simplified formats like `1d` (1 day), `2h` (2 hours), `30m` (30 minutes), `15s` (15 seconds). |
| `timespan_add(ts1: TimeSpan, ts2: TimeSpan): TimeSpan` | Adds two `TimeSpan` objects together. |
| `timespan_diff(ts1: TimeSpan, ts2: TimeSpan): TimeSpan` | Subtracts one `TimeSpan` from another (`ts1 - ts2`). |
| `ago(timespan: TimeSpan): DateTime` | Returns a `DateTime` object that is the given `TimeSpan` in the past, relative to the current UTC time (`now() - timespan`). |
| `from_now(timespan: TimeSpan): DateTime` | Returns a `DateTime` object that is the given `TimeSpan` in the future, relative to the current UTC time (`now() + timespan`). |

#### Examples

**`parse_date(text: string): DateTime`**
```tinkwell
parse_date("2025-01-15T10:30:00Z") // Returns #2025-01-15T10:30:00Z#
parse_date("2024-12-25") // Returns #2024-12-25T00:00:00Z# (time defaults to midnight UTC)
```

**`format_date(date: DateTime, format: string): string`**
```tinkwell
format_date(now(), "yyyy-MM-dd HH:mm:ss") // Returns "2025-07-11 00:00:00"
```

**`date_add(date: DateTime, timespan: TimeSpan): DateTime`**
```tinkwell
date_add(parse_date("2025-01-01"), parse_timespan("1d")) // Returns #2025-01-02T00:00:00Z#
```

**`parse_timespan(text: string): TimeSpan`**
```tinkwell
parse_timespan("1.12:30:00") // Returns TimeSpan for 1 day, 12 hours, 30 minutes
parse_timespan("90m") // Returns TimeSpan for 1 hour and 30 minutes
```

**`ago(timespan: TimeSpan): DateTime`**
```tinkwell
ago(parse_timespan("1h")) // Returns DateTime 1 hour ago from now
```

[Back to index](#functions)

### String Manipulation

| Function | Description |
| :--- | :--- |
| `concat(string1: string, string2: string): string` | Concatenates two strings. If any argument is `null`, it's treated as an empty string. |
| `join(separator: string, values: Collection<any>): string` | Joins a collection of values into a single string using the specified separator. Non-string values in the collection are converted to strings. |
| `length(text: string): number` | Returns the length of a string. Returns `0` if the input is `null`. |
| `or_empty(text: string): string` | Returns the string itself or an empty string (`""`) if it's `null`. Useful for safely handling potentially null string values. |
| `segment_at(text: string, separator: string, index: number): string` | Splits a string by a `separator` and returns the element at the specified `index` (0-based). |
| `split(text: string, separator: string): Collection<string>` | Splits a string into a collection (array) of substrings based on a `separator`. |
| `to_lower(text: string): string` | Converts a string to lowercase. Returns `null` if the input is `null`. |
| `to_upper(text: string): string` | Converts a string to uppercase. Returns `null` if the input is `null`. |
| `trim(text: string): string` | Removes leading and trailing white-space characters from a string. Returns an empty string (`""`) if the input is `null`. |
| `regex_match(text: string, pattern: string): boolean` | Returns `true` if the `text` matches the given regular expression `pattern`. |
| `regex_extract(text: string, pattern: string, group: number): string` | Extracts a specific capture `group` (1-based index) from a regular expression `match` in the `text`. Returns `null` if no match is found or the group does not exist. |
| `match(text: string, pattern: string): boolean` | Returns `true` if the `text` matches the given wildcard expression `pattern`. |
| `json_encode(text: string): string` | Encodes a string into a JSON string literal. |
| `url_encode(text: string): string` | Encodes a string for use in a URL. |

#### Examples

**`concat(string1: string, string2: string): string`**
```tinkwell
concat("Hello", " World") // Returns "Hello World"
```

**`join(separator: string, values: Collection<any>): string`**
```tinkwell
join(", ", ["apple", "banana", "orange"]) // Returns "apple, banana, orange"
```

**`segment_at(text: string, separator: string, index: number): string`**
```tinkwell
segment_at("apple,banana,orange", ",", 1) // Returns "banana"
```

**`regex_match(text: string, pattern: string): boolean`**
```tinkwell
regex_match("sensor-123", "^sensor-\d+$") // Returns true
```

**`regex_extract(text: string, pattern: string, group: number): string`**
```tinkwell
regex_extract("sensor-ID-123", "sensor-ID-(\d+)", 1) // Returns "123"
```

[Back to index](#functions)

### Collection Handling

| Function | Description |
| :--- | :--- |
| `at(collection: Collection, index: number)` | Returns the element at the specified `index` (0-based) in a `collection`. Returns `null` if the collection is `null` or the index is out of bounds. |
| `count(collection: Collection): number` | Returns the number of elements in a `collection`. Returns `0` if the collection is `null`. |
| `first(collection: Collection)` | Returns the first element of a `collection`. Returns `null` if the collection is `null` or empty. |
| `skip(collection: Collection, count: number): Collection` | Bypasses a specified `count` of elements from the start of a `collection` and returns the remaining elements as a new collection. Returns `null` if the input collection is `null`. |
| `take(collection: Collection, count: number): Collection` | Returns a specified `count` of contiguous elements from the start of a `collection` as a new collection. Returns `null` if the input collection is `null`. |
| `sum(collection: Collection<number>): number` | Calculates the sum of a collection of numbers. Returns `0` if the collection is `null` or empty. |
| `avg(collection: Collection<number>): number` | Calculates the average of a collection of numbers. Returns `0` if the collection is `null` or empty. |
| `any(collection: Collection, predicate: string): boolean` | Returns `true` if any element in the `collection` satisfies the `predicate` (a string containing another subexpression). Within the `predicate` expression, the current item is referred to by the parameter `value`. |
| `all(collection: Collection, predicate: string): boolean` | Returns `true` if all elements in the `collection` satisfy the `predicate` (a string containing another subexpression). Within the `predicate` expression, the current item is referred to by the parameter `value`. |

#### Examples

**`at(collection: Collection, index: number)`**
```tinkwell
at(["a", "b", "c"], 1) // Returns "b"
```

**`sum(collection: Collection<number>): number`**
```tinkwell
sum([1, 2, 3]) // Returns 6
```

**`any(collection: Collection, predicate: string): boolean`**
```tinkwell
any([1, 5, 10], "value > 8") // Returns true (because 10 > 8)
```

**`all(collection: Collection, predicate: string): boolean`**
```tinkwell
all([2, 4, 6], "value % 2 == 0") // Returns true (all are even)
```

[Back to index](#functions)

### Type Conversion

| Function | Description |
| :--- | :--- |
| `cbool(value): boolean` | Converts a value to a boolean. Handles numbers (0 is false, non-zero is true), strings ("true", "false" case-insensitive), and other types. |
| `cdouble(value): number` | Converts a value to a double-precision floating-point number. |
| `cfloat(value): number` | Converts a value to a single-precision floating-point number. |
| `cint(value): number` | Converts a value to an integer. Truncates decimal parts. |
| `clong(value): number` | Converts a value to a long integer. Truncates decimal parts. |
| `cstr(value): string` | Converts a value to its string representation. |

[Back to index](#functions)

### JSON Parsing

These functions are useful for parsing JSON strings, especially in the [MQTT Bridge's mapping file](./MQTT-Bridge.md#mapping-file).

| Function | Description |
| :--- | :--- |
| `json_value(json_string: string, path: string)` | Parses a JSON string, navigates to the specified `path`, and attempts to return the value with its most appropriate primitive type (e.g., number, boolean, string). |
| `json_path(json_string: string, path: string): JsonElement` | Extracts a JSON element (object, array, or primitive value) from a JSON string using a dot-separated `path`. |
| `json_get_string(json_element: JsonElement): string` | Converts a `JsonElement` to a string. |
| `json_get_int(json_element: JsonElement): number` | Converts a `JsonElement` to an integer. |
| `json_get_long(json_element: JsonElement): number` | Converts a `JsonElement` to a long integer. |
| `json_get_single(json_element: JsonElement): number` | Converts a `JsonElement` to a single-precision floating-point number. |
| `json_get_double(json_element: JsonElement): number` | Converts a `JsonElement` to a double-precision floating-point number. |
| `json_get_boolean(json_element: JsonElement): boolean` | Converts a `JsonElement` to a boolean. |

#### Examples

Consider the following JSON string:

```json
{
  "sensor": {
    "id": "temp-01",
    "location": "living_room",
    "readings": [
      {"timestamp": "2025-07-10T10:00:00Z", "temperature": 22.5, "unit": "C"},
      {"timestamp": "2025-07-10T10:05:00Z", "temperature": 22.7, "unit": "C"}
    ],
    "active": true
  },
  "metadata": {
    "version": 1.0
  }
}
```

**`json_value(json_string: string, path: string)`**
```tinkwell
json_value(payload, "sensor.location") // returns "living_room" (string)
json_value(payload, "sensor.readings.0.temperature") // returns 22.5 (double)
```

**`json_path(json_string: string, path: string): JsonElement`**
```tinkwell
json_path(payload, "sensor.readings.0") // Returns the first reading object as a JsonElement
```

[Back to index](#functions)

### Security

| Function | Description |
| :--- | :--- |
| `base64_encode(text: string): string` | Encodes a string to its Base64 representation. |
| `base64_decode(text: string): string` | Decodes a Base64 string. |
| `md5(text: string): string` | Computes the MD5 hash of a string. The result is a hexadecimal string. |
| `sha256(text: string): string` | Computes the SHA256 hash of a string. The result is a hexadecimal string. |

[Back to index](#functions)

## Examples

Here are a few examples of how you might use expressions in Tinkwell:

**Derived Measure:**

```
measure power {
    type: "Power"
    unit: "Watt"
    expression: "voltage * current"
}
```

This defines a new measure called "power" that is calculated by multiplying the "voltage" and "current" measures.

**Conditional Runner:**

```
runner "MySpecialRunner" "path/to/my/runner.exe" if "platform == 'windows' and cpu_architecture == 'x64'"
```

This runner will only be started if the operating system is Windows and the CPU architecture is x64.

```
runner "MySpecialRunner" "path/to/my/runner.exe" if "in(platform, 'linux', 'bsd')"
```

This runner will only be started if the operating system is Linux or BSD.

**Reactor Rule:**

```
signal high_temperature {
    when: "temperature > 100"
}
```

This rule will emit a signal if the "temperature" measure exceeds 100 (in whatever unit of measure it has been configured).
