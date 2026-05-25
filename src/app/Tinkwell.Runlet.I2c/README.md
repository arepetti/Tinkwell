# Tinkwell.Runlet.I2c

Headless runlet that polls I2C devices on a Linux host and feeds raw register values into Tinkwell measures.

## Architecture

Implements `IRunlet`.
The `I2cPollingManager` parses `i2c` blocks from the `.tw` configuration using `I2cConfigParser`, opens I2C bus/device handles via `System.Device.I2c`, and runs a polling loop per bus.
Raw byte reads are decoded via `ByteDecoder` and forwarded to the measures service over gRPC.

## Key types

- `I2cRunlet` — `IRunlet` entry point; registers options and the polling manager.
- `I2cPollingManager` — hosted service that opens I2C devices and runs per-bus polling loops.
- `ByteDecoder` — decodes raw byte buffers into typed numeric values (int8 through float32, big- and little-endian).

## Supported data types

`ByteDecoder` supports: `Int8`, `UInt8`, `Int16BE`, `Int16LE`, `UInt16BE`, `UInt16LE`, `Int32BE`, `Int32LE`, `Float32BE`, `Float32LE`.
Each read specifies the data type and an optional `scale` multiplier.

## Platform

Requires Linux with `/dev/i2c-*` bus access.
The runlet skips execution on non-Linux platforms with an error log.
Intended for single-board computers (Raspberry Pi, BeagleBone, etc.).

## Configuration and usage

See [I2C reference](../../docs/reference/i2c.md) for the full `.tw` configuration syntax and examples.
