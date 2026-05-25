# Configuration

Tinkwell uses a custom `.tw` configuration format, parsed by the `ConfigurationParser<T>` pipeline.

## File format

A `.tw` file is a sequence of **blocks** with properties:

```
runner grpc-store from "Tinkwell.Runner.Grpc.dll" {
    runlet store from "Tinkwell.Runlet.Store.dll" {
        storage = "memory"
    }
}

measure voltage {
    quantity = "ElectricPotential"
    unit = "Volt"
    description = "Input voltage"
}
```

- **Blocks** have a type, a name, optional modifiers (`from "..."`, `with ...`), and a body with properties and nested blocks.
- **Properties** are key-value pairs.
  Values can be strings (quoted or unquoted), numbers, booleans, or parenthesized expressions.
- **Quotes are optional** for simple string values without whitespace or special characters.
- **Comments** — `//` to end of line, and `#` at the **start of a line** (after optional whitespace); both are replaced before the grammar runs, preserving line and column for errors.

## Parsing pipeline

1. **Include resolution** — `include "path"` directives are resolved recursively.
   A source map tracks origins.
2. **Lexing/Parsing** — `TwGrammar` (Parlot) produces a `RawDocument`.
   A **remap** pass then rewrites empty placeholder file paths in source locations to the real file and line in the included tree (so diagnostics match what authors see in each file).
3. **Preprocessing** — `Preprocessor` evaluates:
   - `set variable = value` top-level directives
   - `template name { ... }` definitions
   - the `using template-name` and `if (expr)` modifiers on blocks (the former expands a template body into the block; the latter prunes the block when the condition is false).
     **Template names** in `using` and **interpolated expressions** in `if` are resolved the same way as other values (so `$"…"` in those positions is expanded before the modifier runs).
   - Liquid interpolation in `$"..."` strings (rendered by the [Fluid](https://github.com/sebastienros/fluid) .NET engine)
4. **Transformation** — a derived parser converts the `ConfigDocument` into a typed config object.

## Derived parsers

For example:

| Parser | Config type | Top-level block | Notes |
|--------|-------------|-----------------|-------|
| `EnsembleParser` | `EnsembleConfig` | `runner` | Used by the coordinator; run in lax mode |
| `MeasuresParser` | `MeasuresConfig` | `measure` | Used by the measures config worker |

### Lax mode

`ParserOptions.Lax` is a **hint** on the options record: the **base** `ConfigurationParser<T>` does not use it.
Each **derived** parser that supports non-strict files checks `Options.Lax` in its own `TransformAsync` and decides what to do (for example, skip unknown top-level block types).
In Tinkwell, `EnsembleParser` uses lax mode to ignore top-level blocks it does not own (so `ensemble.tw` can contain blocks meant for other subsystems) while still validating `runner` blocks it does handle.

## Value conversion

`ConfigValueConverter` converts `ConfigValue` nodes to CLR types.
For **enum** targets it first tries a **case-insensitive** match to an enum member name, then, for hyphenated input only, **kebab-case to PascalCase** (e.g. `my-value` → `MyValue`) and a second parse attempt.
Comma-separated names are OR'd for **flags** enums.
Names with underscores or spaces are **not** rewritten unless they already match a member; measure-name normalization in the Tinkwell measures parser is separate (see below).

## Naming normalization (Measures)

In the Tinkwell **measures** configuration parser, quantity types and unit names are normalized to PascalCase so that `electric-potential`, `electric_potential`, `electric potential`, and `ElectricPotential` all resolve the same way for UnitsNet compatibility.
That logic is **internal** to the measures parser, not a public API on this library.
