# Tinkwell.Configuration.Abstractions

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This library can be used independently in any .NET application — no Tinkwell installation required.

Contracts and exception types for the configuration system.

## Key types

- **`IConfigurationParser<T>`** — loads a `.tw` file (via `IFileProvider`) and returns a strongly-typed config object `T`.
  An optional `model` object provides template variables for Liquid preprocessing.
- **`ConfigurationParserExtensions`** — adds `LoadFileAsync` convenience method that wraps a physical file path in an `IFileProvider`.
- **Exceptions** — `ConfigurationSyntaxException` (with diagnostics), `ConfigurationFileNotFoundException`, `ConfigurationConversionException`, and the base `ConfigurationException` (carries file name and line number).

## Cross-project docs

- [Configuration](../../docs/architecture/configuration-internals.md) — the `.tw` format, parsing pipeline, and preprocessor.
