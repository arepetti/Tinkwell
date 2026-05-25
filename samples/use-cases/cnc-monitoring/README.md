# CNC Machine Health Monitoring

Monitor spindle vibration, spindle temperature, and coolant temperature on a CNC machine.
Tinkwell reads sensors directly via Modbus RTU, tracks measures, fires signals when thresholds are exceeded, and logs alerts.
No external bridge scripts are needed.

## Hardware

| Component | Example model | Role |
|-----------|---------------|------|
| Vibration sensor | IFM VVB001 (or Minew S4) | RMS velocity on spindle bearing housing |
| Temperature probes | 2x PT100 RTD + MAX31865 breakout | Spindle motor + coolant reservoir |
| USB-RS485 adapter | FTDI FT232R | Connects Modbus RTU sensors to the gateway |
| Edge gateway | Siemens IOT2050, Moxa UC-8100, or Raspberry Pi 4 | Runs Tinkwell |

The vibration sensor and temperature probes connect to the gateway via Modbus RTU (RS-485).
Tinkwell polls the registers directly using its built-in Modbus client.

## Network diagram

```
  ┌─────────────┐   Modbus RTU   ┌──────────────────────────────────────────┐
  │ VVB001      │───────────────>│                                          │
  │ (vibration) │                │  Edge gateway (Pi / IOT2050)             │
  └─────────────┘                │                                          │
                                 │  ┌────────────────────────────────────┐  │
  ┌─────────────┐   Modbus RTU   │  │ Tinkwell Coordinator               │  │
  │ PT100 x2    │───────────────>│  │ (ensemble.tw)                      │  │
  │ (temps)     │                │  │                                    │  │
  └─────────────┘                │  │  modbus ─> measures ─> signals ─>  │  │
                                 │  │           actions                   │  │
                                 │  └────────────────────────────────────┘  │
                                 └──────────────────────────────────────────┘
```

## Files

| File | Description |
|------|-------------|
| `ensemble.tw` | Tinkwell configuration — runners, Modbus polling, measures, signals, actions |

## Setup

### 1. Wire the sensors

Connect the IFM VVB001 and PT100 probes to the gateway's Modbus RTU port via the USB-RS485 adapter.

### 2. Configure register addresses

Edit `ensemble.tw` and adjust the `modbus cnc-sensors` block:

- `port` — serial port (e.g. `/dev/ttyUSB0` or `COM3`)
- `device 1` — change the slave ID if your sensors use a different address
- Register `address` values — match the Modbus register map of your specific sensor models

### 3. Test connectivity

Before starting the full ensemble, verify that Tinkwell can reach the sensors:

```bash
tw modbus read 0x0000 --count 2 --type float32-be --transport rtu --port /dev/ttyUSB0 --slave 1
```

You should see the current vibration value.

### 4. Start Tinkwell

```bash
tw start ensemble.tw
```

If running from a build output directory (not an installed copy), use `./Tinkwell.Coordinator ensemble.tw` instead.

The Modbus runlet starts polling sensors automatically as part of the ensemble.

### 5. Monitor

Each watch command is blocking and needs its own terminal.

**Terminal 1** — live sensor values:

```bash
tw measures watch
```

**Terminal 2** — signal alerts with audible bell:

```bash
tw signals watch --beep
```

**Any terminal** — one-shot queries:

```bash
tw measures list
tw store get cnc/last-alert
```

When vibration exceeds the critical threshold (7.1 mm/s for 5 seconds) you'll see in Terminal 2:

```
14:23:05.120 SIGNAL vibration-critical  severity=critical
```

## Signal thresholds

These follow ISO 10816 zone boundaries for small machines:

| Signal | Condition | Meaning |
|--------|-----------|---------|
| `vibration-warning` | RMS > 4.5 mm/s for 10s | Zone B/C boundary — schedule inspection |
| `vibration-critical` | RMS > 7.1 mm/s for 5s | Zone C/D boundary — stop machine |
| `spindle-overtemp` | Spindle > 85°C for 60s | Bearing or lubrication issue |
| `coolant-low-temp` | Coolant < 5°C | Coolant heater failure or ambient cold |
| `coolant-high-temp` | Coolant > 45°C | Chiller failure |

## Customization

- **Adjust thresholds** — edit the `signal` blocks in `ensemble.tw`.
- **Switch to TCP** — change `transport = tcp` and set `host`/`tcp-port` for Modbus TCP devices.
- **Add more sensors** — add a `measure` block and a matching `register` block in the `modbus` section.
- **Forward alerts** — add an `action` with `do mqtt-publish` to push alerts to a cloud dashboard.
- **Persist history** — the `record-changes` action writes every value change to the state store with a 24h TTL.
