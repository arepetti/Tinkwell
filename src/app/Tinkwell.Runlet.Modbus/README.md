# Tinkwell.Runlet.Modbus

Headless runlet that polls Modbus devices (TCP and RTU) and feeds register values into Tinkwell measures.

## Configuration

`ModbusConfigParser` (namespace `Tinkwell.Runlet.Modbus.Configuration`) extends `Tinkwell.Configuration.Parser.ConfigurationParser<ModbusConfig>` and maps top-level `modbus` blocks to `ModbusConfig` and `ModbusConnectionDefinition` (RTU/TCP), with nested `device` and `register` definitions.
The [Modbus reference](../../docs/reference/modbus.md) has the full `.tw` syntax, examples, and runlet usage.

## Architecture

Implements `IRunlet`.
The `ModbusPollingManager` parses `modbus` blocks from the `.tw` configuration using `ModbusConfigParser`, creates `ModbusTcpClient` or `ModbusRtuClient` instances from the `Tinkwell.Modbus` library, and runs a polling loop per device.
Decoded values are forwarded to the measures service over gRPC.

## Key types

- `ModbusRunlet` — `IRunlet` entry point; registers options and the polling manager.
- `ModbusPollingManager` — hosted service that creates Modbus clients and runs per-device polling loops.

## Dependencies

- **`Tinkwell.Modbus`** — standalone Modbus client library (TCP/RTU).
  See its [README](../libs/Tinkwell.Modbus/README.md) for protocol details and usage.
- **Measures service** — discovered via `IServiceDiscovery` for register value updates.
