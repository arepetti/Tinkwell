# Tinkwell.Expressions

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This library can be used independently in any .NET 10+ application — no Tinkwell installation required.

A [NCalc](https://ncalc.github.io/ncalc/)-based expression engine ([`NCalcSync`](https://www.nuget.org/packages/NCalcSync)) extended with curated Tinkwell functions, shared parse caching, dependency analysis for chained expressions, and structured errors.

Evaluation is **async-only** (`Task`-based APIs on `IExpressionEvaluator`).

## Requirements

- **.NET 10+** (target framework `net10.0`)

In the Tinkwell solution this package is classified as **Standalone** (`TinkwellPackageGroup`) for release packaging.

## Install

```
dotnet add package Tinkwell.Expressions
```

## Quick start

Create an evaluator, pass named parameters keyed exactly as referenced in the expression (parameter syntax uses square brackets), and evaluate:

```csharp
using Tinkwell.Expressions;

var evaluator = new ExpressionEvaluator();
var parameters = new Dictionary<string, object?> { ["a"] = 3 };
var result = await evaluator.EvaluateAsync("1 + [a] * 2", parameters);
// result is 7 (as boxed int/double depending on NCalc)
```

The parameterless `ExpressionEvaluator()` constructor runs `ExpressionFunctionDiscovery.BuiltIn()` in the ctor chain, so discovery via reflection happens **when constructing** each new instance (built-in registrations are cached per assembly inside discovery).
For trimming or Native AOT, use the constructor that takes `IEnumerable<IExpressionFunction>?` with an explicit function list instead (see below).

## How it fits together

Expression text is parsed and evaluated by NCalc `Expression`.
Tinkwell registers custom functions on that expression and supplies ambient `ExpressionParameterContext` for functions that read parameters from the enclosing evaluation.

## Public API overview

| Type | Role |
|------|------|
| `IExpressionEvaluator` | `EvaluateAsync`, `EvaluateBooleanAsync`, `EvaluateStringAsync` — single entry surface for evaluations. Parameter keys must match the **casing** used in the expression. |
| `ExpressionEvaluator` | Default `IExpressionEvaluator` implementation: NCalc wiring, customs dispatch, timeouts, telemetry. |
| `ExpressionEvaluationOptions` | Optional `Timeout` (caller wait bound; default 5 seconds). |
| `ExpressionParseCache` | Static LRU cache of parsed ASTs keyed by expression string; used internally by `ExpressionEvaluator` and `DependencyWalker<TItem>`. |
| `DependencyWalker<TItem>` | Builds forward/reverse dependency maps and a topological **calculation order** from expressions (Kahn); extracts parameters via cached parse. Throws `CircularDependencyException` when ordering fails. |
| `ExpressionEvaluationException` | Parse/evaluation failures; carries the failing expression text; `InnerException` set when wrapping an underlying fault (often `ArgumentException` from a custom function coercion). |
| `CircularDependencyException` | Emitted when a cycle or blocked ordering prevents a full sort; see `CycleParticipants`. |

## Expression evaluation options

`ExpressionEvaluationOptions` defines `Timeout` (nullable).
If omitted, the effective wait is `DefaultTimeout` (5 seconds).

A non-infinite timeout only limits **how long the caller awaits** the result.
Underlying evaluation may continue on a pool thread afterward; `Timeout` is not a CPU preemption.
If the `CancellationToken` cancels (not timer-only timeout), callers get `OperationCanceledException` — it is **not** wrapped as `ExpressionEvaluationException`.

## Built-in functions (this package)

All functions below live in the `Tinkwell.Expressions.Functions.Builtins` namespace and are discovered by `ExpressionFunctionDiscovery.BuiltIn()`.

Names are `snake_case` for types derived from `ExpressionFunction` (PascalCase class name → `snake_case`, with quirks for acronym-style names — override `Name` when needed).

Variadic `make_json` implements `IExpressionFunction` directly; JSON navigation helpers derive from arity-specific bases.

The `quantity` function is **not** part of this package.
It ships with `Tinkwell.Measures` and appears in hosts that combine packages; see **See also** for the unified language reference.

| Category | Representative functions |
|----------|---------------------------|
| **String** | `trim`, `concat`, `substring` |
| **JSON** | `json_encode`, `json_path`, `json_value`, `make_json` |
| **Date/time** | `now`, `parse_date`, `format_date`, `date_add`, `parse_timespan`, `ago`, `from_now` |
| **Time** | `time` (parses `HH:mm` / `HH:mm:ss` to seconds since midnight) |
| **Collection** | `count`, `at`, `first`, `sum`, `skip`, `take` |
| **Conversion** | `cint`, `cdouble`, `cbool`, `cstr` |
| **Template / formatting** | `format` (fills `{Placeholder}` tokens from current expression parameters) |
| **Security / encoding** | `base64_encode`, `md5`, `sha256` |

Examples in other groups from the same sources: `to_lower`, `regex_match`, `url_encode`, `timespan_add`, `date_diff`.

Full signatures, edge cases, and `quantity` are documented alongside NCalc semantics in **See also** — this README avoids duplicating that catalog.

## Adding custom functions

1. Implement `IExpressionFunction` (see `Invoke` vs `FunctionArgs.Result` on the interface).

2. Prefer deriving from `ExpressionFunction` helpers — `UnaryFunction<T>`, `BinaryFunction<T1, T2>`, `TernaryFunction<…>`, `NullaryFunction` — for fixed arity; coercion helpers use invariant `Convert.ChangeType` via `ExpressionFunction.ChangeType<T>`, mapping bad inputs to `ArgumentException`.

3. Supply functions to `new ExpressionEvaluator(functions)`, or concatenate with `ExpressionFunctionDiscovery.BuiltIn()` if you extend the default set:

```csharp
using System.Collections.Generic;
using Tinkwell.Expressions;
using Tinkwell.Expressions.Functions;

var functions = new List<IExpressionFunction>(ExpressionFunctionDiscovery.BuiltIn())
{
    new MyUpper(),
};
var evaluator = new ExpressionEvaluator(functions);

sealed class MyUpper : UnaryFunction<string>
{
    protected override object? Call(string arg) => arg.ToUpperInvariant();
}
```

`ExpressionFunctionDiscovery` finds every concrete `IExpressionFunction` type that has a public parameterless constructor in a given assembly (**no `[ExpressionFunction]` attribute or similar is used** — discovery is reflection-based naming).
Results are cached per `Assembly` in a static `ConcurrentDictionary`.

Hosts that must avoid reflection should pass explicit instances (as above).

## Dependency walking

Use `DependencyWalker<TItem>` when many named items expose optional expression text and reference each other's names as NCalc parameters (for example derived measures or cascading formulas).

Give a **unique name** selector and an optional **expression text** selector; `Analyze` returns `DependencyAnalysis<TItem>` with `CalculationOrder`, `ForwardDependencies`, and `ReverseDependencies`.
Parameter names are extracted with the shared parse cache; edges only connect **inputs you provided** — external names still appear as dependencies.

On failure, `CircularDependencyException` lists `CycleParticipants`: names that remained blocked after the topological walk (a **superset** of a minimal cycle — treat as a group to unblock, not a single ordered loop).

## Errors and `InnerException`

Inside `ExpressionEvaluator`’s core evaluation path, any `Exception` that is **not already** an `ExpressionEvaluationException` becomes `ExpressionEvaluationException` with `InnerException` set to that exception (`OutOfMemoryException` fast-fails before wrapping).

So `ArgumentException` (including arity and coercion errors from `ExpressionFunction` subclasses) surfaces as `ExpressionEvaluationException` with the original `ArgumentException` nested.

Timeout via `ExpressionEvaluationOptions.Timeout` throws `ExpressionEvaluationException` whose message describes the elapsed wait; `InnerException` is `null` for that path.

`OperationCanceledException` from caller cancellation propagates unchanged (not wrapped).
The outer shell around timed evaluation rethrows other exceptions from the inner `Task` without wrapping them in a second `ExpressionEvaluationException`.

## Parse cache and thread safety

`ExpressionParseCache`

- LRU keyed by expression string (`StringComparer.Ordinal`), default `Capacity` `256`; set `Capacity` `0` to disable caching (`Clear` evicts explicitly).

- **Parse failures are not cached** — invalid expressions pay parse cost each time.

- Cached values are `LogicalExpression` ASTs (treated immutable); **each evaluation** builds a new NCalc `Expression` wrapper and attaches parameters/handlers independently.

`ExpressionEvaluator` is documented as safe for **concurrent** calls across threads (no shared per-evaluator parameter state when each call passes its own parameter dictionary).
Do not mutate shared `object` graphs across overlapping evaluations unless those objects are themselves thread-safe.

## Observability

Evaluation and parse-cache hits/misses record metrics and traced spans via internal **`OtMetrics`** and **`OtTraces`**.

Hosted apps should enable OTLP (or another exporter) for those sources — meter and span names used here are summarized in [Telemetry catalog](https://github.com/arepetti/Tinkwell/blob/main/docs/reference/telemetry.md).

## See also

- [Expressions user guide — full language, function catalog, `quantity`, examples](https://github.com/arepetti/Tinkwell/blob/main/docs/user-guide/expressions.md)

- [NCalc project site](https://ncalc.github.io/ncalc/) — underlying grammar and built-in `Abs`, trig, etc.; `IgnoreCaseAtBuiltInFunctions` applies to **NCalc** built-ins, not `[parameter]` names nor Tinkwell custom names (ordinal, case-sensitive).
