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

### General Purpose

#### `if(condition: boolean, true_value: any, false_value: any): any`
Returns `true_value` if `condition` is true, otherwise `false_value`.

**Examples:**
```tinkwell
if(temperature > 100, "High", "Normal") // Returns "High" if temperature > 100, else "Normal"
if(is_null(sensor_data), "No data", sensor_data.value) // Handles null sensor data
```

#### `in(value: any, item1: any, item2: any, ...items: any[]): boolean`
Checks if `value` is present in the provided list of items.

**Examples:**
```tinkwell
in(status, "active", "running") // Returns true if status is "active" or "running"
in(sensor_id, 101, 105, 203) // Returns true if sensor_id is 101, 105, or 203
```

#### `is_null(value: any): boolean`
Returns `true` if the value is `null`.

**Examples:**
```tinkwell
is_null(optional_setting) // Returns true if optional_setting is null
if(is_null(user_input), "Input required", "Input received")
```

#### `is_null_or_empty(text: string | null): boolean`
Returns `true` if the given string is `null` or an empty string (`""`).

**Examples:**
```tinkwell
is_null_or_empty(user_name) // Returns true if user_name is null or ""
```

#### `is_null_or_white_space(text: string | null): boolean`
Returns `true` if the given string is `null`, empty (`""`), or consists only of white-space characters.

**Examples:**
```tinkwell
is_null_or_white_space(user_input) // Returns true if user_input is null, "", or "   "
```

### Math

#### `abs(x: number): number`
Returns the absolute value of a number.

**Examples:**
```tinkwell
abs(-10) // Returns 10
abs(5)   // Returns 5
```

#### `acos(x: number): number`
Returns the angle whose cosine is the specified number. The result is in radians.

**Examples:**
```tinkwell
acos(0.5) // Returns ~1.047 (radians)
```

#### `asin(x: number): number`
Returns the angle whose sine is the specified number. The result is in radians.

**Examples:**
```tinkwell
asin(0.5) // Returns ~0.523 (radians)
```

#### `atan(x: number): number`
Returns the angle whose tangent is the specified number. The result is in radians.

**Examples:**
```tinkwell
atan(1) // Returns ~0.785 (radians)
```

#### `ceiling(x: number): number`
Returns the smallest integer greater than or equal to the specified number.

**Examples:**
```tinkwell
ceiling(4.2)  // Returns 5
ceiling(-4.8) // Returns -4
```

#### `cos(x: number): number`
Returns the cosine of the specified angle (in radians).

**Examples:**
```tinkwell
cos(0) // Returns 1
cos(pi() / 2) // Returns ~0 (where pi() is a hypothetical constant)
```

#### `exp(x: number): number`
Returns `e` raised to the power of the specified number.

**Examples:**
```tinkwell
exp(1) // Returns ~2.718 (e)
exp(0) // Returns 1
```

#### `floor(x: number): number`
Returns the largest integer less than or equal to the specified number.

**Examples:**
```tinkwell
floor(4.8)  // Returns 4
floor(-4.2) // Returns -5
```

#### `ieee_remainder(x: number, y: number): number`
Returns the remainder resulting from the division of `x` by `y` as specified by the IEEE 754 standard.

**Examples:**
```tinkwell
ieee_remainder(10, 3) // Returns 1
ieee_remainder(10, 4) // Returns 2
```

#### `log(x: number): number`
Returns the natural (base `e`) logarithm of a specified number.

**Examples:**
```tinkwell
log(exp(5)) // Returns 5
```

#### `log10(x: number): number`
Returns the base 10 logarithm of a specified number.

**Examples:**
```tinkwell
log10(100) // Returns 2
```

#### `max(x: number, y: number): number`
Returns the larger of two numbers.

**Examples:**
```tinkwell
max(10, 20) // Returns 20
max(current_value, threshold)
```

#### `min(x: number, y: number): number`
Returns the smaller of two numbers.

**Examples:**
```tinkwell
min(10, 20) // Returns 10
min(current_value, upper_limit)
```

#### `pow(x: number, y: number): number`
Returns `x` raised to the power of `y`.

**Examples:**
```tinkwell
pow(2, 3) // Returns 8 (2 * 2 * 2)
pow(base_value, exponent)
```

#### `round(x: number): number`
Rounds a number to the nearest integer. Midpoint values are rounded to the nearest even number (e.g., 2.5 rounds to 2, 3.5 rounds to 4).

**Examples:**
```tinkwell
round(2.4) // Returns 2
round(2.5) // Returns 2
round(3.5) // Returns 4
```

#### `sign(x: number): number`
Returns a value indicating the sign of a number: -1 if negative, 0 if zero, 1 if positive.

**Examples:**
```tinkwell
sign(-5) // Returns -1
sign(0)  // Returns 0
sign(10) // Returns 1
```

#### `sin(x: number): number`
Returns the sine of the specified angle (in radians).

**Examples:**
```tinkwell
sin(0) // Returns 0
sin(pi() / 2) // Returns 1
```

#### `sqrt(x: number): number`
Returns the square root of a specified number.

**Examples:**
```tinkwell
sqrt(9) // Returns 3
sqrt(value)
```

#### `tan(x: number): number`
Returns the tangent of the specified angle (in radians).

**Examples:**
```tinkwell
tan(0) // Returns 0
```

#### `truncate(x: number): number`
Calculates the integer part of a specified number by discarding any fractional digits.

**Examples:**
```tinkwell
truncate(4.8)  // Returns 4
truncate(-4.2) // Returns -4
```

### Date & Time

#### `now(): DateTime`
Returns the current UTC date and time.

**Examples:**
```tinkwell
now() // Returns current UTC DateTime, e.g., #2025-07-10T14:30:00Z#
```

#### `parse_date(text: string): DateTime`
Parses a string into a `DateTime` object. The string must be in a valid date/time format (e.g., ISO 8601).

**Examples:**
```tinkwell
parse_date("2025-01-15T10:30:00Z") // Returns #2025-01-15T10:30:00Z#
parse_date("2024-12-25") // Returns #2024-12-25T00:00:00Z# (time defaults to midnight UTC)
```
**Errors:** Throws an error if the string cannot be parsed into a valid date.

#### `format_date(date: DateTime, format: string): string`
Formats a `DateTime` object into a string using a specified format. The format string uses standard .NET custom date and time format specifiers.

**Examples:**
```tinkwell
format_date(now(), "yyyy-MM-dd HH:mm:ss") // Returns "2025-07-10 14:30:00"
format_date(parse_date("2025-01-01"), "MM/dd/yyyy") // Returns "01/01/2025"
```
**Reference:** For format specifiers, refer to the .NET documentation on "Custom date and time format strings".

#### `date_diff(d1: DateTime, d2: DateTime): TimeSpan`
Calculates the duration (`TimeSpan`) between two `DateTime` objects (`d1 - d2`).

**Examples:**
```tinkwell
date_diff(now(), parse_date("2025-07-10T14:00:00Z")) // Returns a TimeSpan representing 30 minutes
```

#### `date_add(date: DateTime, timespan: TimeSpan): DateTime`
Adds a `TimeSpan` to a `DateTime` object.

**Examples:**
```tinkwell
date_add(parse_date("2025-01-01"), parse_timespan("1d")) // Returns #2025-01-02T00:00:00Z#
date_add(now(), parse_timespan("2h30m")) // Returns DateTime 2 hours and 30 minutes from now
```

#### `year(date: DateTime): number`
Extracts the year component from a `DateTime` object.

**Examples:**
```tinkwell
year(now()) // Returns 2025
```

#### `month(date: DateTime): number`
Extracts the month component (1-12) from a `DateTime` object.

**Examples:**
```tinkwell
month(now()) // Returns 7 (for July)
```

#### `day(date: DateTime): number`
Extracts the day of the month component (1-31) from a `DateTime` object.

**Examples:**
```tinkwell
day(now()) // Returns 10
```

#### `hour(date: DateTime): number`
Extracts the hour component (0-23) from a `DateTime` object.

**Examples:**
```tinkwell
hour(now()) // Returns 14 (for 2 PM UTC)
```

#### `minute(date: DateTime): number`
Extracts the minute component (0-59) from a `DateTime` object.

**Examples:**
```tinkwell
minute(now()) // Returns 30
```

#### `second(date: DateTime): number`
Extracts the second component (0-59) from a `DateTime` object.

**Examples:**
```tinkwell
second(now()) // Returns 0
```

#### `parse_timespan(text: string): TimeSpan`
Creates a `TimeSpan` object from a string. Supports standard `d.hh:mm:ss` format and simplified formats like `1d` (1 day), `2h` (2 hours), `30m` (30 minutes), `15s` (15 seconds).

**Examples:**
```tinkwell
parse_timespan("1.12:30:00") // Returns TimeSpan for 1 day, 12 hours, 30 minutes
parse_timespan("2h") // Returns TimeSpan for 2 hours
parse_timespan("90m") // Returns TimeSpan for 1 hour and 30 minutes
parse_timespan("5s") // Returns TimeSpan for 5 seconds
```
**Errors:** Throws an error if the string cannot be parsed into a valid `TimeSpan`.

#### `timespan_add(ts1: TimeSpan, ts2: TimeSpan): TimeSpan`
Adds two `TimeSpan` objects together.

**Examples:**
```tinkwell
timespan_add(parse_timespan("1h"), parse_timespan("30m")) // Returns TimeSpan for 1 hour 30 minutes
```

#### `timespan_diff(ts1: TimeSpan, ts2: TimeSpan): TimeSpan`
Subtracts one `TimeSpan` from another (`ts1 - ts2`).

**Examples:**
```tinkwell
timespan_diff(parse_timespan("2h"), parse_timespan("30m")) // Returns TimeSpan for 1 hour 30 minutes
```

#### `ago(timespan: TimeSpan): DateTime`
Returns a `DateTime` object that is the given `TimeSpan` in the past, relative to the current UTC time (`now() - timespan`).

**Examples:**
```tinkwell
ago(parse_timespan("1h")) // Returns DateTime 1 hour ago from now
```

#### `from_now(timespan: TimeSpan): DateTime`
Returns a `DateTime` object that is the given `TimeSpan` in the future, relative to the current UTC time (`now() + timespan`).

**Examples:**
```tinkwell
from_now(parse_timespan("2d")) // Returns DateTime 2 days from now
```

### String Manipulation

#### `concat(string1: string | null, string2: string | null): string`
Concatenates two strings. If any argument is `null`, it's treated as an empty string.

**Examples:**
```tinkwell
concat("Hello", " World") // Returns "Hello World"
concat("Value: ", sensor_value) // Concatenates a string literal with a variable
```

#### `join(separator: string, values: Collection<any>): string`
Joins a collection of values into a single string using the specified separator. Non-string values in the collection are converted to strings.

**Examples:**
```tinkwell
join(", ", ["apple", "banana", "orange"]) // Returns "apple, banana, orange"
join(" - ", sensor_readings) // Joins numeric sensor readings with " - "
```

#### `length(text: string | null): number`
Returns the length of a string. Returns `0` if the input is `null`.

**Examples:**
```tinkwell
length("Tinkwell") // Returns 8
length(null) // Returns 0
```

#### `or_empty(text: string | null): string`
Returns the string itself or an empty string (`""`) if it's `null`. Useful for safely handling potentially null string values.

**Examples:**
```tinkwell
or_empty(optional_description) // Returns "" if optional_description is null
```

#### `segment_at(text: string, separator: string, index: number): string`
Splits a string by a `separator` and returns the element at the specified `index` (0-based).

**Examples:**
```tinkwell
segment_at("apple,banana,orange", ",", 1) // Returns "banana"
```
**Errors:** Throws an error if the `index` is out of bounds or the `separator` is not found.

#### `split(text: string, separator: string): Collection<string>`
Splits a string into a collection (array) of substrings based on a `separator`.

**Examples:**
```tinkwell
split("item1;item2;item3", ";") // Returns ["item1", "item2", "item3"]
```

#### `to_lower(text: string | null): string | null`
Converts a string to lowercase. Returns `null` if the input is `null`.

**Examples:**
```tinkwell
to_lower("HELLO World") // Returns "hello world"
```

#### `to_upper(text: string | null): string | null`
Converts a string to uppercase. Returns `null` if the input is `null`.

**Examples:**
```tinkwell
to_upper("hello World") // Returns "HELLO WORLD"
```

#### `trim(text: string | null): string`
Removes leading and trailing white-space characters from a string. Returns an empty string (`""`) if the input is `null`.

**Examples:**
```tinkwell
trim("  hello  ") // Returns "hello"
trim(null) // Returns ""
```

#### `regex_match(text: string, pattern: string): boolean`
Returns `true` if the `text` matches the given regular expression `pattern`.

**Examples:**
```tinkwell
regex_match("sensor-123", "^sensor-\d+$") // Returns true
regex_match("data-abc", "^sensor-\d+$") // Returns false
```
**Reference:** Uses .NET regular expression syntax.

#### `regex_extract(text: string, pattern: string, group: number): string | null`
Extracts a specific capture `group` (1-based index) from a regular expression `match` in the `text`. Returns `null` if no match is found or the group does not exist.

**Examples:**
```tinkwell
regex_extract("sensor-ID-123", "sensor-ID-(\d+)", 1) // Returns "123"
regex_extract("no-match", "(\d+)", 1) // Returns null
```

### Collection Handling

#### `at(collection: Collection<any> | null, index: number): any | null`
Returns the element at the specified `index` (0-based) in a `collection`. Returns `null` if the collection is `null` or the index is out of bounds.

**Examples:**
```tinkwell
at(["a", "b", "c"], 1) // Returns "b"
at(sensor_readings, 0) // Returns the first reading
at(["x"], 5) // Returns null (index out of bounds)
```

#### `count(collection: Collection<any> | null): number`
Returns the number of elements in a `collection`. Returns `0` if the collection is `null`.

**Examples:**
```tinkwell
count(["a", "b", "c"]) // Returns 3
count(empty_list) // Returns 0
count(null) // Returns 0
```

#### `first(collection: Collection<any> | null): any | null`
Returns the first element of a `collection`. Returns `null` if the collection is `null` or empty.

**Examples:**
```tinkwell
first(["a", "b", "c"]) // Returns "a"
first(empty_list) // Returns null
```

#### `skip(collection: Collection<any> | null, count: number): Collection<any> | null`
Bypasses a specified `count` of elements from the start of a `collection` and returns the remaining elements as a new collection. Returns `null` if the input collection is `null`.

**Examples:**
```tinkwell
skip(["a", "b", "c", "d"], 2) // Returns ["c", "d"]
skip(["a", "b"], 5) // Returns [] (empty collection)
```

#### `take(collection: Collection<any> | null, count: number): Collection<any> | null`
Returns a specified `count` of contiguous elements from the start of a `collection` as a new collection. Returns `null` if the input collection is `null`.

**Examples:**
```tinkwell
take(["a", "b", "c", "d"], 2) // Returns ["a", "b"]
take(["a", "b"], 5) // Returns ["a", "b"] (returns all available elements)
```

#### `sum(collection: Collection<number> | null): number`
Calculates the sum of a collection of numbers. Returns `0` if the collection is `null` or empty.

**Examples:**
```tinkwell
sum([1, 2, 3]) // Returns 6
sum(sensor_values)
```

#### `avg(collection: Collection<number> | null): number`
Calculates the average of a collection of numbers. Returns `0` if the collection is `null` or empty.

**Examples:**
```tinkwell
avg([10, 20, 30]) // Returns 20
avg(daily_temperatures)
```

#### `any(collection: Collection<any> | null, predicate: string): boolean`
Returns `true` if any element in the `collection` satisfies the `predicate` (a string containing another subexpression). Within the `predicate` expression, the current item is referred to by the parameter `it`.

**Examples:**
```tinkwell
any([1, 5, 10], "value > 8") // Returns true (because 10 > 8)
any(sensor_readings, "[value.status] == 'error'") // Checks if any reading has an error status
```
**Note:** The `predicate` is a string that will be evaluated as a separate expression for each item. Use `value` to access the current item.

#### `all(collection: Collection<any> | null, predicate: string): boolean`
Returns `true` if all elements in the `collection` satisfy the `predicate` (a string containing another subexpression). Within the `predicate` expression, the current item is referred to by the parameter `it`.

**Examples:**
```tinkwell
all([2, 4, 6], "value % 2 == 0") // Returns true (all are even)
all(sensor_readings, "value > 0") // Checks if all readings are positive
```
**Note:** The `predicate` is a string that will be evaluated as a separate expression for each item. Use `value` to access the current item.

### Type Conversion

#### `cbool(value: any): boolean`
Converts a value to a boolean. Handles numbers (0 is false, non-zero is true), strings ("true", "false" case-insensitive), and other types.

**Examples:**
```tinkwell
cbool(1) // Returns true
cbool("false") // Returns false
cbool(0) // Returns false
```

#### `cdouble(value: any): number`
Converts a value to a double-precision floating-point number.

**Examples:**
```tinkwell
cdouble("123.45") // Returns 123.45
cdouble(10) // Returns 10.0
```

#### `cfloat(value: any): number`
Converts a value to a single-precision floating-point number.

**Examples:**
```tinkwell
cfloat("123.45") // Returns 123.45 (as a float)
```

#### `cint(value: any): number`
Converts a value to an integer. Truncates decimal parts.

**Examples:**
```tinkwell
cint("123") // Returns 123
cint(123.99) // Returns 123
```

#### `clong(value: any): number`
Converts a value to a long integer. Truncates decimal parts.

**Examples:**
```tinkwell
clong("9876543210") // Returns 9876543210 (as a long)
```

#### `cstr(value: any): string`
Converts a value to its string representation.

**Examples:**
```tinkwell
cstr(123) // Returns "123"
cstr(true) // Returns "True"
```

### JSON Parsing

These functions are useful for parsing JSON strings, especially in the [MQTT Bridge's mapping file](./MQTT-Bridge.md#mapping-file).

#### `json_value(json_string: string, path: string): string | number | boolean | null`
Parses a JSON string, navigates to the specified `path`, and attempts to return the value with its most appropriate primitive type (e.g., number, boolean, string). This function simplifies common cases but might require explicit type conversion if the inferred type is not what's expected. For example, a JSON number `123` will be returned as a numeric type, but if you need it as a string `"123"`, you'll need to use `cstr(json_value(...))`.

**Path Syntax:**
The `path` uses dot notation for object properties (e.g., `"sensor.location"`) and numeric indices for array elements (e.g., `"readings.0.temperature"`).

**Examples:**
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

You can extract values using `json_value()`:

*   **Accessing object properties:**
    `json_value(payload, "sensor.location")` returns `"living_room"` (string)
    `json_value(payload, "sensor.active")` returns `true` (boolean)
    `json_value(payload, "metadata.version")` returns `1.0` (double)

*   **Accessing array elements by index:**
    `json_value(payload, "sensor.readings.0.temperature")` returns `22.5` (double)
    `json_value(payload, "sensor.readings.1.timestamp")` returns `"2025-07-10T10:05:00Z"` (string)

*   **Combining with other functions:**
    `parse_date(json_value(payload, "sensor.readings.0.timestamp"))` returns a `DateTime` object.
    `cstr(json_value(payload, "sensor.id"))` returns `"temp-01"` (string)

**Errors:** Throws an error if the JSON is invalid, a path segment is not found, or an array index is out of bounds.

#### `json_path(json_string: string, path: string): JsonElement`
Extracts a JSON element (object, array, or primitive value) from a JSON string using a dot-separated `path`. This function returns a raw `JsonElement` object, which can then be passed to other `json_get_*` functions for specific type conversions. See `json_value()` for examples of path syntax.

**Examples:**
```tinkwell
json_path(payload, "sensor.readings.0") // Returns the first reading object as a JsonElement
json_get_double(json_path(payload, "sensor.readings.0.temperature")) // Explicitly gets the temperature as a double
```
**Errors:** Throws an error if the JSON is invalid, a path segment is not found, or an array index is out of bounds.

#### `json_get_string(json_element: JsonElement): string`
Converts a `JsonElement` to a string.

**Examples:**
```tinkwell
json_get_string(json_path(payload, "sensor.id")) // Returns "temp-01"
```

#### `json_get_int(json_element: JsonElement): number`
Converts a `JsonElement` to an integer.

**Examples:**
```tinkwell
json_get_int(json_path(payload, "metadata.version")) // Returns 1
```

#### `json_get_long(json_element: JsonElement): number`
Converts a `JsonElement` to a long integer.

**Examples:**
```tinkwell
json_get_long(json_path(payload, "large_number_field"))
```

#### `json_get_single(json_element: JsonElement): number`
Converts a `JsonElement` to a single-precision floating-point number.

**Examples:**
```tinkwell
json_get_single(json_path(payload, "sensor.readings.0.temperature")) // Returns 22.5 (as a float)
```

#### `json_get_double(json_element: JsonElement): number`
Converts a `JsonElement` to a double-precision floating-point number.

**Examples:**
```tinkwell
json_get_double(json_path(payload, "sensor.readings.0.temperature")) // Returns 22.5 (as a double)
```

#### `json_get_boolean(json_element: JsonElement): boolean`
Converts a `JsonElement` to a boolean.

**Examples:**
```tinkwell
json_get_boolean(json_path(payload, "sensor.active")) // Returns true
```

### Security

#### `base64_encode(text: string): string`
Encodes a string to its Base64 representation.

**Examples:**
```tinkwell
base64_encode("Hello World") // Returns "SGVsbG8gV29ybGQ="
```

#### `base64_decode(text: string): string`
Decodes a Base64 string.

**Examples:**
```tinkwell
base64_decode("SGVsbG8gV29ybGQ=") // Returns "Hello World"
```

#### `md5(text: string): string`
Computes the MD5 hash of a string. The result is a hexadecimal string.

**Examples:**
```tinkwell
md5("Tinkwell") // Returns "d41d8cd98f00b204e9800998ecf8427e" (example hash)
```

#### `sha256(text: string): string`
Computes the SHA256 hash of a string. The result is a hexadecimal string.

**Examples:**
```tinkwell
sha256("Tinkwell") // Returns "a591a6d40bf420404a011733cfb7b190d62c65bf0bcda32b57b277d9ad9f146e" (example hash)
```

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
