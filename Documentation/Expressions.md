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

| Function | Description |
| --- | --- |
| `Abs(x)` | Absolute value |
| `Acos(x)` | Arc cosine |
| `Asin(x)` | Arc sine |
| `Atan(x)` | Arc tangent |
| `Ceiling(x)` | Smallest integer greater than or equal to `x` |
| `Cos(x)` | Cosine |
| `Exp(x)` | Exponential (e^`x`) |
| `Floor(x)` | Largest integer less than or equal to `x` |
| `IEEERemainder(x, y)`| IEEE 754-style remainder of `x` divided by `y` |
| `Log(x)` | Natural logarithm (base e) |
| `Log10(x)` | Base-10 logarithm |
| `Max(x, y)` | Maximum of two values |
| `Min(x, y)` | Minimum of two values |
| `Pow(x, y)` | `x` raised to the power `y` |
| `Round(x)` | Round `x` to the nearest integer |
| `Sign(x)` | Returns -1, 0, or 1 depending on the sign of `x` |
| `Sin(x)` | Sine |
| `Sqrt(x)` | Square root |
| `Tan(x)` | Tangent |
| `Truncate(x)` | Integer part of `x` (truncates fractional digits) |
| `in(x, a, b, ...)` | Checks if `x` appears in the given list |
| `if(condition, a, b)`| Returns `a` if `condition` is true, otherwise `b` |

Additional functions may be available in specific contexts.

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
