# Lab instrument monitoring

Continuously monitors benchtop test instruments (DMM, power supply) via SCPI commands over TCP, plus the host machine's CPU temperature via Linux sysfs.
Signals fire when readings drift outside tolerances; actions log warnings and push webhooks.

## Hardware

- **Keysight 34461A** (or compatible) digital multimeter with LAN interface (SCPI over TCP on port 5025).
- **Rigol DP832** (or compatible) programmable DC power supply with LAN interface (SCPI over TCP on port 5555).
- Both instruments connected to the same LAN as the Tinkwell host.
- The Tinkwell host is a Linux machine (for sysfs CPU temperature).

## Network diagram

```
┌──────────────────────┐
│  Tinkwell host       │
│  (Linux PC / RPi)    │
│                      │
│  Tinkwell.Runlet     │
│  .TextQuery          │
│    ├─ TCP → DMM      │──── LAN ──── Keysight 34461A (192.168.1.50:5025)
│    ├─ TCP → PSU      │──── LAN ──── Rigol DP832    (192.168.1.51:5555)
│    └─ File → sysfs   │
│                      │
│  Measures → Signals  │
│          → Actions   │
└──────────────────────┘
```

## Files

| File | Description |
|------|-------------|
| `ensemble.tw` | Complete Tinkwell configuration |
| `README.md` | This file |

## How it works

1. The **text-query** runlet opens TCP connections to the DMM and power supply.
2. Every 500 ms (DMM) or 1 second (PSU), it sends a SCPI measurement command (e.g. `MEAS:VOLT:DC?`) and reads the response.
3. A regex extracts the numeric value from the ASCII response.
4. The value is pushed to the corresponding Tinkwell measure.
5. A separate file-based query reads `/sys/class/thermal/thermal_zone0/temp` every 5 seconds, divides by 1000 (millidegrees to degrees), and updates `cpu-temp`.
6. Signals evaluate expressions against measure values and fire when thresholds are crossed.
7. Actions log alerts and send HTTP POST webhooks.

## Quick start

### 1. Configure instrument IPs

Edit `ensemble.tw` and set the IP addresses and ports of your instruments:

```tw
query dmm {
    host = "192.168.1.50"   # your DMM IP
    port = 5025
    ...
}

query psu {
    host = "192.168.1.51"   # your PSU IP
    port = 5555
    ...
}
```

### 2. Verify connectivity

Test SCPI connectivity with `netcat` (or similar):

```bash
echo "MEAS:VOLT:DC?" | nc 192.168.1.50 5025
# Should print something like: +4.98765432E+00
```

### 3. Start Tinkwell

```bash
tw start samples/use-cases/lab-instruments/ensemble.tw
```

If running from a build output directory (not an installed copy), use `./Tinkwell.Coordinator samples/use-cases/lab-instruments/ensemble.tw` instead.

### 4. Monitor values (separate terminals)

```bash
# Terminal 2 — live measure values
tw measures watch

# Terminal 3 — signal alerts (with audible bell)
tw signals watch --beep
```

## Signal thresholds

| Signal | Condition | Severity |
|--------|-----------|----------|
| `voltage-out-of-range` | board-voltage outside 4.5–5.5 V | Critical |
| `high-current` | board-current-mA > 500 mA | Warning |
| `cpu-hot` | cpu-temp > 80 °C | Warning |

## Customization

- **Add more instruments**: duplicate a `query` block with different host/port and SCPI commands.
- **Serial instruments**: change `transport = serial`, add `serial-port` and `baudrate` properties for RS-232 instruments.
- **Shell scripts**: use `transport = command` to run any script and extract values from its output.
- **Windows**: remove the `host-sensors` query (sysfs is Linux-specific) or replace it with a command transport using `wmic` or `powershell`.
