# DUT thermal protection (closed-loop)

A closed-loop protection system that reads a device-under-test (DUT) surface temperature from a digital multimeter and automatically shuts down the power supply channel when the temperature exceeds a safe threshold.
When the DUT cools back down, the power supply is re-enabled.

This example demonstrates the **read from one instrument, write to another** pattern using the TextQuery runlet (read) and the `text-send` action (write), both communicating via SCPI over TCP.

## Hardware

- **Keysight 34461A** (or compatible DMM) with thermocouple measurement capability, connected via LAN (SCPI over TCP on port 5025).
- **Type-K thermocouple** attached to the DUT surface, connected to the DMM.
- **Rigol DP832** (or compatible programmable DC power supply) connected via LAN (SCPI over TCP on port 5555), powering the DUT.
- All instruments and the Tinkwell host on the same network.

## How it works

```
                     ┌──────────────────┐
                     │   Tinkwell host   │
                     │                  │
  ┌──────────┐  TCP  │  TextQuery       │  TCP   ┌──────────┐
  │ Keysight │◄─────│  (read temp)     │──────►│ Rigol    │
  │ 34461A   │      │                  │       │ DP832    │
  │ + TC     │      │  Measures →      │       │          │
  └────┬─────┘      │  Signals →       │       └────┬─────┘
       │            │  text-send       │            │
       │            │  (OUTP CH1,OFF)  │            │
  thermocouple      └──────────────────┘        DC power
       │                                            │
       └──────────── DUT ───────────────────────────┘
```

1. The **text-query** runlet polls the DMM every 500 ms, sending `MEAS:TEMP:TCO? K` and extracting the temperature reading.
2. It also polls the PSU every second for actual voltage and current output.
3. All readings are pushed to Tinkwell **measures**.
4. **Signals** evaluate:
   - `overtemp` fires when `dut-temp > 85 °C`, clears at `<= 75 °C`
   - `warm` fires at `> 70 °C` as an early warning
5. When `overtemp` **fires**, the `shutdown-psu` action sends `OUTP CH1,OFF` to the power supply via `text-send`.
6. When `overtemp` **clears** (DUT cooled to safe range), the `restore-psu` action sends `OUTP CH1,ON` to re-enable the supply.
7. All signal transitions are logged and persisted for audit.

## Files

| File | Description |
|------|-------------|
| `ensemble.tw` | Complete Tinkwell configuration |
| `README.md` | This file |

## Quick start

### 1. Configure instrument IPs

Edit `ensemble.tw` and set the IP addresses and ports:

```tw
query dmm {
    host = "192.168.1.50"   # your DMM IP
    port = 5025
    ...
}

query psu-monitor {
    host = "192.168.1.51"   # your PSU IP
    port = 5555
    ...
}
```

Update the same PSU address in the `shutdown-psu` and `restore-psu` actions.

### 2. Verify SCPI connectivity

```bash
# Test DMM temperature reading
echo "MEAS:TEMP:TCO? K" | nc 192.168.1.50 5025
# Should return something like: +2.54321000E+01

# Test PSU output control
echo "OUTP CH1,ON" | nc 192.168.1.51 5555
```

### 3. Start Tinkwell

```bash
tw start samples/use-cases/dut-protection/ensemble.tw
```

If running from a build output directory (not an installed copy), use `./Tinkwell.Coordinator samples/use-cases/dut-protection/ensemble.tw` instead.

### 4. Monitor (separate terminals)

```bash
# Terminal 2 — live temperature and PSU readings
tw measures watch

# Terminal 3 — signal alerts with audible bell
tw signals watch --beep

# Terminal 4 — audit trail
tw events list --last 20
```

## Signal thresholds

| Signal | Fires | Clears | Severity | Action |
|--------|-------|--------|----------|--------|
| `warm` | dut-temp > 70 °C | dut-temp <= 65 °C | Warning | Log only |
| `overtemp` | dut-temp > 85 °C | dut-temp <= 75 °C | Critical | PSU CH1 OFF / ON |

The 10 °C hysteresis between fire and clear thresholds prevents rapid toggling of the power supply when the temperature hovers near the limit.

## Customization

- **Adjust thresholds**: change the `when`/`until` values in the signal blocks.
- **Multiple channels**: duplicate the `text-send` blocks to control additional PSU channels (`CH2`, `CH3`).
- **Voltage ramp-down**: instead of a hard shutdown, send a series of `VOLT:LEV` commands to gradually reduce voltage.
- **Serial instruments**: change `transport = serial` and set `serial-port` / `baudrate` for RS-232 connected instruments.
- **Webhook alerts**: add a `do http-post` block to notify external systems.
