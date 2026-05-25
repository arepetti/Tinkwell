# Water quality analysis bench

> **Linux only.** The I2C runlet requires `/dev/i2c-*` bus access and is intended for quick tests and examples, not production use.

A QC lab runs periodic water quality checks.
Three Atlas Scientific EZO sensors (pH, dissolved oxygen, conductivity) sit on an I2C bus connected to a Raspberry Pi.
Tinkwell reads raw sensor values directly over I2C, evaluates water quality signals, and alerts on out-of-spec conditions.

## Hardware

- **Raspberry Pi 4** (or any Linux SBC with I2C)
- **Atlas Scientific EZO-pH** — pH probe circuit (I2C address `0x63`)
- **Atlas Scientific EZO-DO** — dissolved oxygen circuit (I2C address `0x61`)
- **Atlas Scientific EZO-EC** — conductivity circuit (I2C address `0x64`)
- **Carrier board** (e.g. Whitebox Labs Tentacle T3) to host the EZO circuits

## Network diagram

```
┌──────────────────────────┐
│    Raspberry Pi 4        │
│                          │
│  Tinkwell.Runlet.I2c     │
│    bus 1 ──┬── 0x63 pH   │─── Atlas EZO-pH + probe
│            ├── 0x61 DO   │─── Atlas EZO-DO + probe
│            └── 0x64 EC   │─── Atlas EZO-EC + probe
│                          │
│  Measures → Signals      │
│          → Actions       │
└──────────────────────────┘
```

## Files

| File | Description |
|------|-------------|
| `ensemble.tw` | Complete Tinkwell configuration |
| `README.md` | This file |

## How it works

1. The **I2C runlet** opens three devices on bus 1 at addresses `0x63`, `0x61`, and `0x64`.
2. Every 2 seconds, it writes the register address byte (`0x00`) and reads 4 bytes (IEEE 754 float, big-endian) from each sensor.
3. Decoded values are pushed to Tinkwell **measures**: `ph`, `dissolved-oxygen`, `conductivity`.
4. **Signals** evaluate water quality:
   - `ph-out-of-range` fires when pH < 6.5 or > 8.5
   - `low-dissolved-oxygen` fires when DO < 4 mg/L
   - `high-conductivity` fires when EC > 1500 µS/cm
5. **Actions** log alerts and send HTTP POST webhooks to a LIMS.

## Quick start

### 1. Enable I2C on the Raspberry Pi

```bash
sudo raspi-config
# Interface Options → I2C → Enable
sudo usermod -aG i2c $USER
# Log out and back in
```

### 2. Verify sensors are detected

```bash
i2cdetect -y 1
# Should show devices at 0x61, 0x63, 0x64
```

### 3. Start Tinkwell

```bash
tw start samples/use-cases/water-quality/ensemble.tw
```

If running from a build output directory (not an installed copy), use `./Tinkwell.Coordinator samples/use-cases/water-quality/ensemble.tw` instead.

### 4. Monitor (separate terminals)

```bash
# Terminal 2 — live sensor readings
tw measures watch

# Terminal 3 — water quality alerts
tw signals watch --beep

# Terminal 4 — audit trail
tw events list --last 20
```

## Signal thresholds

| Signal | Fires | Clears | Severity |
|--------|-------|--------|----------|
| `ph-out-of-range` | pH < 6.5 or > 8.5 | pH 6.5–8.5 | Critical |
| `low-dissolved-oxygen` | DO < 4 mg/L | DO >= 4.5 mg/L | Critical |
| `high-conductivity` | EC > 1500 µS/cm | EC <= 1400 µS/cm | Warning |

## Important notes

- **Sensor calibration**: Atlas EZO sensors require calibration before use.
  Follow the Atlas Scientific calibration procedures for each sensor.
  This is outside the scope of Tinkwell.
- **Register protocol**: This example assumes sensors are in I2C polling mode and return a 4-byte float at register `0x00`.
  The actual EZO I2C protocol involves sending a read command (`0x52`) and waiting for the response.
  For real deployment, a custom runlet implementing the full EZO protocol would be more appropriate.
- **Not for production**: The I2C runlet performs simple register reads.
  Production water quality monitoring should use a dedicated runlet with proper command sequencing, calibration support, and error recovery.

## Customization

- **Additional sensors**: add more `device` blocks with the sensor's I2C address and register layout.
- **Different bus**: change `bus = 1` to the appropriate bus number.
- **Temperature compensation**: Atlas EZO sensors support temperature compensation — this would require write support (not available in the basic I2C runlet).
- **MQTT forwarding**: add an MQTT runlet and publish readings to a broker for integration with other systems.
