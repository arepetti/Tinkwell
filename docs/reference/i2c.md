# I2C integration

> **Linux only.** This runlet uses `/dev/i2c-*` bus access via the `System.Device.I2c` library.
> It is intended for quick tests and examples on single-board computers (Raspberry Pi, BeagleBone, etc.) — not for serious production use.

The **I2C** runlet polls devices on an I2C bus, reads raw bytes from specified registers, decodes them into numeric values, and feeds them into Tinkwell measures.
No sensor-specific logic is included — you specify addresses, register offsets, byte lengths, and data types.

## Configuration

Declare one or more `i2c` blocks in your `.tw` file.
Each block represents one I2C bus with one or more `device` sub-blocks.

```tw
i2c water-sensors {
    bus = 1
    poll-interval = "1 second"

    device 0x63 {
        read ph {
            register = 0x00
            length = 4
            type = float32-be
            measure = ph
        }
    }

    device 0x61 {
        read dissolved-oxygen {
            register = 0x00
            length = 4
            type = float32-be
            measure = dissolved-oxygen
        }
    }

    device 0x64 {
        read conductivity {
            register = 0x00
            length = 4
            type = float32-be
            scale = 0.001
            measure = conductivity
        }
    }
}
```

## Bus properties

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `bus` | No | `1` | I2C bus number (maps to `/dev/i2c-N`) |
| `poll-interval` | No | `1 second` | How often to poll (e.g. `500 ms`, `2 seconds`) |

## Device block

The device block name is the 7-bit I2C address in hex (`0x63`) or decimal (`99`).

## Read properties

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `register` | No | `0x00` | Register address byte to write before reading |
| `length` | No | auto | Number of bytes to read (inferred from `type` if omitted) |
| `type` | No | `uint8` | Data type for decoding (see table below) |
| `scale` | No | `1.0` | Multiplier applied to the decoded value |
| `measure` | No | block name | Name of the Tinkwell measure to update |

## Data types

| Type | Bytes | Description |
|------|-------|-------------|
| `int8` | 1 | Signed 8-bit integer |
| `uint8` / `byte` | 1 | Unsigned 8-bit integer |
| `int16-be` | 2 | Signed 16-bit, big-endian |
| `int16-le` | 2 | Signed 16-bit, little-endian |
| `uint16-be` | 2 | Unsigned 16-bit, big-endian |
| `uint16-le` | 2 | Unsigned 16-bit, little-endian |
| `int32-be` | 4 | Signed 32-bit, big-endian |
| `int32-le` | 4 | Signed 32-bit, little-endian |
| `float32-be` | 4 | IEEE 754 single-precision, big-endian |
| `float32-le` | 4 | IEEE 754 single-precision, little-endian |

## Runlet setup

```tw
runner background from "Tinkwell.Runner.Headless.dll" {
    runlet i2c     from "Tinkwell.Runlet.I2c.dll";
    runlet actions from "Tinkwell.Runlet.Actions.dll";
}
```

## How it works

1. The runlet opens each I2C device using `I2cDevice.Create()` from the `System.Device.I2c` library.
2. For each `read` block, it writes the register address byte then reads `length` bytes from the device.
3. The raw bytes are decoded according to the `type` and multiplied by `scale`.
4. The result is pushed as a Tinkwell measure via gRPC.
5. After all reads on all devices, the runlet sleeps for `poll-interval`.

## Prerequisites

- **Linux** with I2C enabled (e.g. `raspi-config` on Raspberry Pi).
- The user running Tinkwell must have access to `/dev/i2c-*` (typically the `i2c` group: `sudo usermod -aG i2c $USER`).
- **Not supported on Windows or macOS.**

## Limitations

- Raw byte reads only — no sensor-specific command sequences, calibration, or multi-step read protocols.
- Single register-address byte (8-bit).
  Devices requiring 16-bit register addresses need a custom runlet.
- No write support — read-only polling.
- Intended for examples and quick prototyping.
  For production I2C integration, consider a dedicated runlet with sensor-specific logic.
