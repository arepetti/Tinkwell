# Conventions

Code and project conventions used across the Tinkwell solution.

## Project naming

- `Tinkwell.{Concern}` — core libraries (e.g., `Tinkwell.Core`, `Tinkwell.Measures`).
- `Tinkwell.{Concern}.Abstractions` — abstractions (e.g., `Tinkwell.Configuration.Abstractions`).
- `Tinkwell.{Concern}.{Sub}` — focused libraries (e.g., `Tinkwell.Configuration.Parser`, `Tinkwell.Configuration.Measures`).
- `Tinkwell.Runner.{Variant}` — runner hosts (`Grpc`, `Headless`, `Hosting`, `Abstractions`).
- `Tinkwell.Runlet.{Name}` — runlet implementations (`Store`, `Measures`).
- `Tinkwell.{Project}.Tests` — test projects mirror their source project name.

## Type naming suffixes

| Suffix | Meaning |
|--------|---------|
| `Runner` | A runner builder (e.g., `GrpcRunnerBuilder`) |
| `Runlet` | An `IRunlet` implementation (e.g., `MeasuresRunlet`) |
| `Descriptor` | An immutable record describing a runtime entity, typically received from the coordinator or config (e.g., `RunnerDescriptor`, `RunletDescriptor`) |
| `Options` | Mutable configuration/settings classes bound from config sections or command-line args (e.g., `CoordinatorOptions`, `TlsOptions`, `RestartPolicyOptions`, `EndpointOptions`) |
| `Command` | A pipe command handler or CLI command (e.g., `ConfigPathCommand`, `StoreListCommand`) |
| `Service` | A gRPC service implementation (e.g., `StateStoreService`, `MeasuresGrpcService`) |
| `Worker` | A `BackgroundService` (e.g., `DerivedMeasureWorker`, `NotificationWorker`) |
| `Parser` | A `ConfigurationParser<T>` derivative (e.g., `EnsembleParser`, `MeasuresParser`) |
| `Registry` | A runtime registry (e.g., `MeasureRegistry`, `RunnerRegistry`, `ServiceRegistry`) |
| `Factory` | Creates instances with async setup (e.g., `MeasureRegistryFactory`) |
| `Holder` | Bridges async initialization with synchronous DI (e.g., `MeasureRegistryHolder`) |
| `Backend` | A storage/persistence implementation behind an interface (e.g., `MemoryStoreBackend`, `SqliteStoreBackend`) |
| `Notifier` | Manages event fan-out to subscribers (e.g., `StoreNotifier`) |
| `Config` | Immutable record produced by a parser, representing the parsed content of a `.tw` file or block (e.g., `EnsembleConfig`, `RunnerConfig`, `MeasuresConfig`, `BucketConfig`) |

### `Descriptor` vs `Options`

Both are data containers, but they serve different roles:

- **Descriptor** — immutable (`sealed record`), describes something that already exists.
  Created once and never mutated.
  Example: `RunnerDescriptor` carries the runner's ID, name, and resolved settings.
- **Options** — mutable (`sealed class`), configures something that will be created.
  Typically bound from an `IConfiguration` section or from `IOptions<T>`.
  Example: `EndpointOptions` holds `BasePort` and `PortRange` for the endpoint allocator.

## The `Ot` prefix (OpenTelemetry)

Each project that emits telemetry has two static classes following the `Ot` prefix convention:

- **`OtMetrics`** — defines `Meter`, `Counter`, and `Histogram` instances for that assembly's metrics.
  The meter name matches the project namespace (e.g., `Tinkwell.Coordinator`).
- **`OtTraces`** — defines an `ActivitySource` and string constants for span names and tag keys.
  The source name also matches the project namespace.

These classes are always `internal static` and are referenced only within their own project.
The `Ot` prefix keeps them short and visually distinct from domain types.

## Null checks and patterns

- Prefer `is not null` and `is null` over `!= null` / `== null`.
- For null-check-and-bind, use typed patterns: `if (def.Minimum is double min)` rather than `is { } min`.
- Property patterns (`is { IsClass: true }`) are fine when checking properties.

## Configuration naming

- Settings in `.tw` files use **kebab-case**: `calculated-measures`, `expiration-interval-seconds`.
- Quantity types and unit names are **normalized to PascalCase** from any input format (kebab, snake, space-separated, or PascalCase).

## Central Package Management

All NuGet package versions are centralized in `Directory.Packages.props` at the solution root.
Individual `.csproj` files reference packages without `Version=` attributes.
Transitive pinning is enabled (`CentralPackageTransitivePinningEnabled`) to prevent version drift.

## gRPC service placement

All gRPC service implementations live in a `Grpc` subfolder and the `{ProjectNamespace}.Grpc` namespace within their runlet project.
The proto-generated types share the same namespace (set via `csharp_namespace` in the `.proto` file).
This keeps gRPC plumbing separated from domain logic.

- `Tinkwell.Runlet.Store.Grpc.StateStoreService`
- `Tinkwell.Runlet.Measures.Grpc.MeasuresGrpcService`

## Visibility on internal types

When a class is `internal`, its members should be declared as if the class were `public` (`public`, `protected`, etc.).
This way, promoting the class to `public` later requires changing only the class declaration.
Avoid marking members `internal` when the containing type is already `internal`.

## DI patterns

- Runlets register their services in `ConfigureServices` and `MapGrpcServices`.
- For types that need async initialization, use a `Holder` class with a `TaskCompletionSource` — register the holder as a singleton, then `Set` it during `StartAsync`.
- `BackgroundService` / `IHostedService` for long-running work.

## Test conventions

- Test project names mirror source projects with a `.Tests` suffix.
- xUnit with `[Fact]` and `[Theory]`.
  Test `.tw` files go in a `TestFiles` directory.
- `InternalsVisibleTo` is used sparingly for testing internal helpers (e.g., `NormalizeToPascalCase`).

## Loop style

- No spaces around `=` in the `for` initializer: `for (int i=0; …)` not `for (int i = 0; …)`.
- Prefix increment/decrement in the iterator: `++i` / `--i`, not `i++` / `i--`.

```csharp
for (int i=0; i < limit; ++i)
```

## Control-flow block formatting

- **No single-line `if`/`else`/`catch`/`finally` bodies.** The body must start on a new line.
- For simple `return`, `continue`, `break`, `throw`, or short single-statement bodies, braces are optional but the statement must still be on its own line, indented.
- For anything more complex (assignments, method calls with side effects, multi-expression lines), always wrap in `{ }`.

```csharp
// OK — simple return on its own line
if (disposed)
    return;

// OK — braces for a non-trivial body
if (value is null)
{
    _logger.LogWarning("Value was null");
    return;
}

// WRONG — single-line if
if (disposed) return;
```

- **`catch` blocks always use braces**, even for a single `throw;` or `return`.
  The opening `{` goes on the next line (Allman style).

```csharp
catch (OperationCanceledException)
{
    throw;
}
```

## Exception handling

- **Never catch bare `Exception` without guarding against fatal exceptions.** `OutOfMemoryException`, `StackOverflowException`, and similar fatal CLR exceptions must not be silently swallowed.
- When catching `Exception`, either:
  1. Add a separate `catch` for fatal exceptions before the general `catch (Exception)`, or
  2. Re-check inside the handler and fast-fail.

Preferred pattern — catch fatal exceptions first and fast-fail:

```csharp
catch (OutOfMemoryException)
{
    Environment.FailFast("Out of memory");
}
catch (Exception ex)
{
    _logger.LogError(ex, "…");
}
```

- The `Environment.FailFast` call attempts to write the message to the Windows Application event log (or stderr on Linux) and terminates the process immediately without running finalizers.
  This is intentional for unrecoverable conditions.
- If a `catch (Exception)` is in a context where `OperationCanceledException` should propagate (e.g., inside a `BackgroundService`), always add `catch (OperationCanceledException) { throw; }` before the general handler.

## Documentation line breaks

Markdown source files use **semantic line breaks**: one sentence per line, with no hard wrap at a fixed column.
The renderer (browser, GitHub, docfx) handles reflow at the actual viewport width.

- Do not hard-wrap paragraphs at 80 (or any) characters; bare newlines force breaks in some renderers and look ragged on wide screens.
- Continuation sentences inside list items are indented to align with the first sentence.
- Code blocks, tables, HTML blocks and front matter are left untouched.
- The line-length lint is disabled for `*.md` in both `.editorconfig` (`max_line_length = off`) and `.markdownlint.json` (`MD013: false`).
- A reflow helper lives at `scripts/reflow-markdown.mjs`; it is idempotent and can be run on individual files or via `--all` to reformat the in-scope set.

## Editor and line endings

Code style and line endings are enforced by `.editorconfig` and `.gitattributes` at the repository root.

## Build output

All projects output to a shared `artifacts/` directory.
This is relevant for runlet assembly loading — the `RunletLoader` installs an assembly resolver that probes this directory for dependencies.
