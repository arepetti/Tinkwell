# Text Query integration

The **TextQuery** runlet is a generic data acquisition runlet that polls text-based data sources, extracts numeric values with regular expressions, and feeds them into Tinkwell measures.
It supports four transports:

| Transport | Description | Typical use |
|-----------|-------------|-------------|
| `tcp` | TCP socket (send command, read response) | SCPI instruments, NMEA GPS receivers |
| `serial` | Serial port (RS-232 / RS-485) | AT modems, serial instruments |
| `file` | Read a file on disk | Linux sysfs (`/sys/class/thermal`), `/proc` |
| `command` | Execute a shell command, capture stdout | `nvidia-smi`, `sensors`, custom scripts |

## Configuration

Declare one or more `query` blocks in your `.tw` file.
Each block represents one data source with one or more `read` sub-blocks for individual values.

### TCP (SCPI instrument)

```tw
query dmm {
    transport = tcp
    host = "192.168.1.50"
    port = 5025
    poll-interval = "500 ms"

    read dc-voltage {
        send = "MEAS:VOLT:DC?"
        pattern = "([+-]?[0-9.Ee+-]+)"
        measure = board-voltage
    }

    read dc-current {
        send = "MEAS:CURR:DC?"
        pattern = "([+-]?[0-9.Ee+-]+)"
        scale = 1000
        measure = board-current-mA
    }
}
```

### Serial

```tw
query gps {
    transport = serial
    serial-port = "/dev/ttyUSB1"
    baudrate = 9600
    poll-interval = "1 second"

    read latitude {
        send = ""
        pattern = "\\$GPGGA,\\d+\\.\\d+,([0-9.]+),[NS]"
        measure = gps-latitude
    }
}
```

### File (Linux sysfs)

```tw
query cpu-temp {
    transport = file
    path = "/sys/class/thermal/thermal_zone0/temp"
    poll-interval = "5 seconds"

    read temp {
        pattern = "(\\d+)"
        scale = 0.001
        measure = cpu-temperature
    }
}
```

### Command (shell)

```tw
query gpu {
    transport = command
    command = "nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader,nounits"
    poll-interval = "10 seconds"

    read temp {
        pattern = "(\\d+)"
        measure = gpu-temperature
    }
}
```

## Connection properties

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `transport` | Yes | — | `tcp`, `serial` (alias: `rtu`), `file`, or `command` (aliases: `cmd`, `exec`) |
| `host` | TCP only | — | Hostname or IP address |
| `port` | TCP only | `5025` | TCP port number |
| `serial-port` | Serial only | — | Serial port name (e.g. `/dev/ttyUSB0`, `COM3`) |
| `baudrate` | Serial only | `9600` | Baud rate |
| `path` | File only | — | Absolute file path |
| `command` | Command only | — | Shell command to execute |
| `line-terminator` | No | `lf` | Line terminator: `lf`, `cr`, `crlf`, or `none` |
| `read-timeout` | No | `2000` | Timeout in milliseconds for reading a response |
| `poll-interval` | No | `1 second` | How often to poll (e.g. `500 ms`, `2 seconds`) |

## Read properties

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `send` | No | — | Command string to send before reading (TCP/serial only) |
| `pattern` | Yes | — | Regex pattern; the first (or specified) capture group is extracted |
| `group` | No | `1` | Which capture group to use (1-based) |
| `scale` | No | `1.0` | Multiplier applied to the extracted value |
| `measure` | No | block name | Name of the Tinkwell measure to update |

## Runlet setup

```tw
runner background from "Tinkwell.Runner.Headless.dll" {
    runlet text-query from "Tinkwell.Runlet.TextQuery.dll";
    runlet actions    from "Tinkwell.Runlet.Actions.dll";
}
```

## How it works

1. The runlet connects to each data source using the configured transport.
2. For each `read` block, it optionally sends the `send` command, then reads the response.
3. The response is matched against the `pattern` regex.
4. The captured group value is parsed as a number and multiplied by `scale`.
5. The result is pushed as a Tinkwell measure via gRPC.
6. After all reads, the runlet sleeps for `poll-interval` and repeats.

For file and command transports, `send` is ignored — the entire file content or command output is read and matched against the pattern.

**TCP read behavior:** The TCP transport reads in a loop until the configured line terminator is detected, the `read-timeout` expires, the remote side closes the connection, or 64 KiB of data has been accumulated — whichever comes first.
If the timeout fires before a terminator, the partial response is still matched against the `pattern`.

## See also

- [Actions](actions.md) — The `text-send` handler writes data back over TCP, serial, or file as the outbound counterpart to TextQuery polling.
