# Tinkwell.Configuration.Parser

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This library can be used independently in any .NET 10+ application — no Tinkwell installation required.

Small DSL parser for the Tinkwell `.tw` configuration format.
Parlot-based grammar, include resolution with source mapping, and Fluid-driven preprocessing (`set`, templates, conditionals on blocks, interpolated values) run before your code maps the AST to a typed result.

In the Tinkwell solution this package is classified as **SDK** (`TinkwellPackageGroup`) for release packaging.

## Install

```
dotnet add package Tinkwell.Configuration.Parser
```

The package pulls in [`Tinkwell.Configuration.Abstractions`](https://www.nuget.org/packages/Tinkwell.Configuration.Abstractions).

That dependency supplies `ConfigValue`, `IConfigurationParser<T>`, `ConfigurationParserExtensions.LoadFileAsync`, and configuration exceptions.

## Requirements

- **.NET 10+** (target framework `net10.0`).

## Quick start

`ConfigurationParser<T>` is abstract: loading always runs the full pipeline, then calls your `TransformAsync`.
To obtain an unmodified `ConfigDocument`, subclass once and pass the document through:

```csharp
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

public sealed class RawTwParser : ConfigurationParser<ConfigDocument>
{
    protected override ValueTask<ConfigDocument> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
        => ValueTask.FromResult(document);
}

var parser = new RawTwParser();

// Absolute or relative path; relative paths resolve from the current working directory.
ConfigDocument doc = await parser.LoadFileAsync("ensemble.tw");

foreach (var block in doc.Blocks)
{
    Console.WriteLine($"{block.Type} {block.Name} @ {block.Location}");
    foreach (var prop in block.Properties)
        Console.WriteLine($"  {prop.Key} = {prop.Value}");
}
```

A minimal `.tw` snippet illustrating blocks and properties (see the [configuration guide](https://github.com/arepetti/Tinkwell/blob/main/docs/user-guide/configuration.md) for full syntax):

```
runner grpc-store from "Tinkwell.Runner.Grpc.dll" {
    runlet store from "Tinkwell.Runlet.Store.dll" {
        storage = "memory"
    }
}

measure temperature {
    quantity = Temperature
    unit = Celsius
}
```

## Public API overview

All types below live in `Tinkwell.Configuration.Parser` unless noted.

### `ConfigurationParser<T>` and `IConfigurationParser<T>`

`ConfigurationParser<T>` orchestrates loading: include resolution → Parlot parse → preprocessing → `TransformAsync`.

Implement `protected abstract ValueTask<T> TransformAsync(ConfigDocument document, CancellationToken cancellationToken)` to produce your domain config. Constructors optionally take `ILogger`, `ParserOptions`, and a `Tinkwell.Expressions.IExpressionEvaluator` (defaults to a reflective evaluator unless you supply one — pass an explicit evaluator for trimming/AOT-sensitive apps).

**`LoadAsync(IFileProvider, string path, object? model, CancellationToken)`** implements **`Tinkwell.Configuration.IConfigurationParser<T>`** from the abstractions package.
Use **`ConfigurationParserExtensions.LoadFileAsync`** from that package when you load from a file path.

**`CancellationToken`** is honoured during include resolution and preprocessing; cancellation surfaces as **`OperationCanceledException`** (or **`TaskCanceledException`** depending on host defaults).

### `ConfigDocument`

Immutable root after preprocessing: ordered top-level **`Blocks`**, plus optional **`Warnings`** (`ConfigurationDiagnostic`) for non-fatal issues (such as skipped duplicate includes).

### `ConfigBlock`

One block **`type`** (keyword), **`name`**, **`Modifiers`** (`from "…"`, custom pairs), **`Properties`** in the body, nested **`Children`**, and a **`Location`** on the header for diagnostics.
Reserved modifiers **`if`** and **`using`** are consumed during preprocessing and do not remain on delivered blocks.

### `Property`

A **`Key`**, **`Value`** (`ConfigValue` from abstractions), and **`Location`** on the assignment.

### `Modifier`

Keyword–value pairs between the name and **`{`** (e.g. `from "dll"`).
Interpretation is domain-specific.

### `SourceLocation`

**`FilePath`**, **`Line`**, **`Column`** (1-based) pointing into the logical source tree (includes remapped positions).

### `ParserOptions`

**`Lax`** is a **hint** for derived parsers: when `true`, `TransformAsync` implementations may tolerate unknown block types or other content intended for another consumer.
The base **`ConfigurationParser<T>`** does **not** enforce lax behavior during parse or preprocessing — only your override decides what “unknown” means.

### `ErrorPolicy`

Strongly typed result of **`on error`** configuration (terminal **`ErrorPolicyAction`**, optional **`RetryPolicy`**, **`Publish`** naming and payloads).
Consumers that parse those blocks populate this record; it is not the same knob as **`ParserOptions.Lax`**.

### `ConfigValueConverter`

Static helpers to turn **`ConfigValue`** instances into CLR types (`ConvertTo<T>`, `ConvertTo` with `SourceLocation`).
When a **`SourceLocation`** is supplied, failures become **`ConfigurationConversionException`** (from **`Tinkwell.Configuration.Abstractions`**) carrying file and line metadata.

## The pipeline

1. **`IncludeResolver`** resolves `include "path"` recursively (relative paths are anchored to the containing file), tracks a **source map**, and merges text.
Duplicate includes are skipped with warnings.

2. **`TwGrammar`** (Parlot) parses the merged text into a raw AST after comment stripping.

3. **`RawAstRemapper`** rewrites placeholder locations through the map so diagnostics point at real files/lines.

4. **`Preprocessor`** applies **`set`** variables, model properties, Fluid rendering on interpolated values, **`template`** expansion, **`using`** merges, NCalc-backed **`if`** conditions on blocks, and produces **`ConfigDocument`**.

Internals (`TwGrammar`, `Preprocessor`, `IncludeResolver`, …) are **`internal`**; extend behavior only by **`ConfigurationParser<T>`** and **`ParserOptions`** (used in your **`TransformAsync`**).

## Subclassing

Subclasses hold domain logic inside **`TransformAsync`**.

Traverse **`ConfigDocument`**, validate block types you own, resolve **`Modifier`**/`Property` values (`ConfigValueConverter` or bespoke handling), and return your immutable **`...Config`** record or graph.

```csharp
public sealed record MyRuntimeConfig(bool EnableThing, int Port);

public sealed class MyTwParser : ConfigurationParser<MyRuntimeConfig>
{
    public MyTwParser(ParserOptions? options = null) : base(options: options)
    {
    }

    protected override ValueTask<MyRuntimeConfig> TransformAsync(
        ConfigDocument document, CancellationToken ct)
    {
        var svc = document.Blocks
            .FirstOrDefault(b => b.Type == "myservice");

        if (svc is null)
            throw new InvalidOperationException("Missing 'myservice' block.");

        var portProp = svc.Properties.Single(p => p.Key == "port");
        var port = ConfigValueConverter.ConvertTo<int>(
            portProp.Value, portProp.Location);

        return ValueTask.FromResult(new MyRuntimeConfig(
            EnableThing: svc.Properties.Any(p => p.Key == "enable"),
            Port: port));
    }
}
```

## Error handling

**Parse and preprocess:** failures surface as subclasses of **`Tinkwell.Configuration.ConfigurationException`**.
Those types are defined in the **`Tinkwell.Configuration`** namespace in the **`Tinkwell.Configuration.Abstractions`** assembly (the NuGet package name; not a C# namespace named `Abstractions`).

- **`ConfigurationSyntaxException`** — grammar or preprocessor rules (often with multiple **`ConfigurationDiagnostic`** entries).
- **`ConfigurationFileNotFoundException`** — an included path could not be opened through the **`IFileProvider`**.
- **`ConfigurationConversionException`** — **`ConfigValueConverter`** could not coerce a value.

**`TransformAsync`:** your code may throw as needed; the base class does not translate those.

**Warnings:** non-fatal issues (e.g. duplicate includes) appear in **`ConfigDocument.Warnings`** and are also logged at warning level when a logger is supplied.

**Strict vs lax:** default options are **strict** in the sense that **`ParserOptions.Lax`** is `false`.
Setting **`Lax = true`** does not change the core pipeline; only your **`TransformAsync`** (or higher-level wrappers) should read **`Options.Lax`** and decide whether to ignore unknown blocks or similar.

## Source positions

**`SourceLocation`** is attached to block headers and properties so you can report **`file:line:column`** in errors.
The include pass and remap keep positions aligned with the files authors edit, not only the merged buffer.

## Includes

Use top-of-file style **`include "relative-or-logical-path"`** (double-quoted path, line format expected by the resolver).
Paths are resolved **relative to the directory of the file that contains the directive**, then read through **`IFileProvider`**.
Recursive includes are supported; the same file is only inlined once (subsequent includes become warnings).
Missing files throw **`ConfigurationFileNotFoundException`**.

## Liquid preprocessing

The Parlot grammar pass runs first so blocks and assignments exist as structure.
After that, **Fluid** renders **`$"…"`** interpolated strings and any other string value that contains **`{{`** (Liquid-style **`{{ var }}`** and tags the Fluid parser accepts in that template) using **`set`** variables and **`LoadAsync`** model properties.
Block-level structural filtering uses the **`if (expression)`** modifier with the expression evaluator, not Liquid **`{% if %}`** inside the grammar.

This split keeps authoring flexible: environment-specific text layers on top without hand-copying entire files.

## Observability

Loads and preprocessing emit OpenTelemetry **`Meter`** and **`Activity`** data via internal **`OtMetrics`** and **`OtTraces`**.

Meter and activity names are summarized in [Telemetry catalog](https://github.com/arepetti/Tinkwell/blob/main/docs/reference/telemetry.md) for apps that configure OTLP export.

## See also

- [Configuration internals (architecture)](https://github.com/arepetti/Tinkwell/blob/main/docs/architecture/configuration-internals.md) — pipeline details, lax mode in derived parsers, and value-conversion notes.
- [Tinkwell configuration user guide](https://github.com/arepetti/Tinkwell/blob/main/docs/user-guide/configuration.md) — `.tw` syntax, blocks, includes, variables, and templates for authors.

## Tests

The mirror project is **`src/tests/Tinkwell.Configuration.Parser.Tests`**.
From the `src/` directory run **`dotnet test Tinkwell.slnx`**; see **[Build](https://github.com/arepetti/Tinkwell/blob/main/AGENTS.md#build)** in **`AGENTS.md`** for restore/build.
Alternatively **`cd src && dotnet test tests/Tinkwell.Configuration.Parser.Tests`** scopes the run to this project only.
