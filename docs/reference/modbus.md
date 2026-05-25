# Modbus Integration

Tinkwell includes a built-in Modbus client that polls RTU (serial/RS-485) and TCP devices, decoding register values and feeding them directly into the measures system.
No external bridge scripts are needed.

## Configuration

Declare `modbus` blocks in your `.tw` ensemble file:

### RTU (serial)

```tw
modbus cnc-sensors {
    transport = rtu
    port = "/dev/ttyUSB0"
    baudrate = 9600

    device 1 {
        poll-interval = "1 second"

        register spindle-vibration {
            address = 0x0000
            type = float32-be
        }

        register spindle-temp {
            address = 0x0010
            type = int16
            scale = 0.1
        }
    }
}
```

### TCP

```tw
modbus plc {
    transport = tcp
    host = "192.168.1.100"
    tcp-port = 502

    device 1 {
        poll-interval = "500 ms"

        register output-pressure {
            address = 100
            type = float32-be
        }
    }
}
```

## Connection properties

| Property | Default | Description |
|----------|---------|-------------|
| `transport` | `rtu` | `rtu` (serial) or `tcp` |
| `port` | — | Serial port name (RTU only, e.g. `/dev/ttyUSB0`, `COM3`) |
| `baudrate` | `9600` | Serial baud rate (RTU only) |
| `host` | `localhost` | TCP hostname or IP (TCP only) |
| `tcp-port` | `502` | TCP port (TCP only) |

## Device properties

| Property | Default | Description |
|----------|---------|-------------|
| (name) | — | Modbus slave ID (1–247) |
| `poll-interval` | `1` (second) | How often to read registers. Accepts `"500 ms"`, `"2 seconds"`, or a plain number (seconds). |

## Register properties

| Property | Default | Description |
|----------|---------|-------------|
| (name) | — | Block name = measure name (unless `measure` is set) |
| `address` | — | Register address (decimal or `0x` hex) |
| `type` | `int16` | Data type for decoding (see table below) |
| `kind` | `holding` | `holding` (FC 03) or `input` (FC 04) |
| `scale` | `1.0` | Multiplier applied after decoding |
| `measure` | (block name) | Measure to update (overrides block name) |

## Data types

| Type string | Registers | Byte order | Description |
|-------------|-----------|------------|-------------|
| `int16` | 1 | — | Signed 16-bit |
| `uint16` | 1 | — | Unsigned 16-bit |
| `int32-be` / `int32` | 2 | ABCD | Signed 32-bit, big-endian |
| `int32-le` | 2 | DCBA | Signed 32-bit, little-endian |
| `uint32-be` / `uint32` | 2 | ABCD | Unsigned 32-bit, big-endian |
| `uint32-le` | 2 | DCBA | Unsigned 32-bit, little-endian |
| `float32-be` / `float32` / `float` | 2 | ABCD | IEEE 754 float, big-endian (most common) |
| `float32-le` | 2 | DCBA | IEEE 754 float, little-endian |
| `float32-ws` / `float32-swapped` | 2 | BADC | IEEE 754 float, word-swapped (Schneider/Modicon PLCs) |

## Runlet setup

Add the Modbus runlet to a headless runner in your ensemble:

```tw
runner background from "Tinkwell.Runner.Headless.dll" {
    runlet modbus from "Tinkwell.Runlet.Modbus.dll";
}
```

The runlet discovers the measures gRPC service automatically.
Make sure the measures runner starts before the Modbus runner (declare it earlier in the ensemble).

## CLI commands

For manual testing and debugging:

```bash
# Read 2 holding registers as a float from a TCP device
tw modbus read 0x0000 --count 2 --type float32-be --host 192.168.1.100

# Read 1 input register via RTU
tw modbus read 0x0010 --transport rtu --port COM3 --slave 1 --input

# Write a single register
tw modbus write 0x0064 1234 --host 192.168.1.100
```

See the [CLI reference](../user-guide/cli.md#modbus) for full options.

## Library API

The `Tinkwell.Modbus` NuGet package provides the standalone protocol library for use outside of Tinkwell.
See the [library README](https://github.com/arepetti/Tinkwell/blob/main/src/app/libs/Tinkwell.Modbus/README.md) for C# examples.
