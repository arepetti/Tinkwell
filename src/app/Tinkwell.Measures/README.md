# Tinkwell.Measures

Domain model for the measures system.
This assembly has no infrastructure dependencies — it defines the types and contracts that the rest of the measures stack builds on.
The store-backed implementation of `IMeasureRegistry` lives in `Tinkwell.Runlet.Measures` (internal to that assembly).

## Key types

- **`IMeasureRegistry`** — the central contract: `RegisterAsync`, `UpdateAsync`, `FindAsync`, `FindAllAsync`, `FindDefinitionAsync`, `WatchAsync`, and a `ValueChanged` event.
- **`MeasureDefinition`** — name, type (`Number`/`String`), quantity type, unit, min/max, precision, TTL, and attributes (`None`, `Constant`, `Derived`).
- **`MeasureValue`** — wraps an `IQuantity` (UnitsNet) or a raw string.
  Factory methods: `FromValue`, `FromQuantity`.
  Accessors: `AsDouble`, `AsString`, `AsQuantity`.
- **`Measure`** — combines `MeasureDefinition`, `MeasureMetadata`, and an optional `MeasureValue`.
- **`MeasureMetadata`** — description, category, tags.
- **`Quant`** — UnitsNet utility: `ParseUnit`, `IsValidUnit`, `ParseAndConvert`, `Round`.

## Cross-project docs

- [Measures system](../../docs/reference/measures.md) — end-to-end flow from config file to runtime values.
