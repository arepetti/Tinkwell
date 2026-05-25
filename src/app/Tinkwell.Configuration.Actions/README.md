# Tinkwell.Configuration.Actions

Parses `action` blocks from `.tw` configuration files.

## What it does

`ActionsParser` extends `ConfigurationParser<ActionsConfig>` and transforms top-level `action` blocks into `ActionDefinition` objects containing event filters (`source`, `verb`), optional `when` guard expressions, and `do` / `on error` handler invocations that can reference built-in actions or external assemblies via `from`.

## Key types

| Type | Role |
|------|------|
| `ActionsParser` | Parser implementation |
| `ActionsConfig` | Root config — `IReadOnlyList<ActionDefinition> Actions` |
| `ActionDefinition` | Named action with filters, guard, and handlers |
| `ActionHandlerDefinition` | `do` or `on error` handler (built-in or `from` assembly) |

## Used by

- [Tinkwell.Runlet.Actions](../Tinkwell.Runlet.Actions/)

## Cross-project docs

- [Actions reference](../../docs/reference/actions.md)
