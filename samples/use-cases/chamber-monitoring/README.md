# Environmental Chamber Monitoring

Monitor temperature and humidity in an ICH stability chamber for pharma/biotech compliance.
Tinkwell acts as a LwM2M server, receives sensor readings from the chamber, tracks measures, fires signals on ICH condition deviations, and maintains a full audit trail via event persistence.
An HTTP webhook action notifies external systems (LIMS, alerting) on excursions.

## Regulatory context

ICH Q1A(R2) defines long-term stability testing at **25°C ± 2°C / 60% RH ± 5% RH**.
Deviations from these setpoints ("excursions") must be detected, recorded, and reported.
This example implements:

- **Critical signals** when temperature or humidity leaves the allowed range for more than 5 minutes (filters door-open transients)
- **Warning signals** at narrower thresholds for early drift detection
- **Immutable event log** (`chamber-events.db`) for the full audit trail — relevant for 21 CFR Part 11 and EU Annex 11

## Hardware

| Component | Example model | Role |
|-----------|---------------|------|
| Stability chamber | Binder KBF 720, Weiss WK3, or Memmert HPP | ICH-compliant climate chamber with LwM2M support |
| Edge gateway | Raspberry Pi 4 or any Linux/Windows machine | Runs Tinkwell on the same network as the chamber |

The chamber exposes standard LwM2M IPSO objects over CoAP/UDP:

| Object ID | Object name | Resource | Description |
|-----------|-------------|----------|-------------|
| 3303 | Temperature | `/3303/0/5700` | Chamber internal temperature (°C) |
| 3304 | Humidity | `/3304/0/5700` | Chamber relative humidity (%) |

No bridge scripts, no MQTT — the LwM2M runlet communicates directly with the chamber.

## Network diagram

```
  ┌──────────────────┐   CoAP/UDP    ┌──────────────────────────────────────┐
  │ Stability Chamber│──────────────>│  Edge gateway (Pi / workstation)     │
  │ (LwM2M client)   │   port 5683   │                                     │
  │                  │               │  ┌──────────────────────────────────┐│
  │  OBJ 3303 Temp   │               │  │ Tinkwell Coordinator             ││
  │  OBJ 3304 RH     │               │  │ (ensemble.tw)                    ││
  │                  │               │  │                                  ││
  └──────────────────┘               │  │  lwm2m -> measures -> signals -> ││
                                     │  │          actions -> webhook       ││
                                     │  │          event persistence (SQLite)│
                                     │  └──────────────────────────────────┘│
                                     └──────────────────────────────────────┘
```

## Files

| File | Description |
|------|-------------|
| `ensemble.tw` | Tinkwell configuration — runners, LwM2M server, measures, signals, actions |

## Setup

### 1. Configure the chamber

Set the chamber's LwM2M client to register with the gateway's IP address on port 5683.
Consult the chamber's manual for the LwM2M configuration screen.
The server URI is typically:

```
coap://<gateway-ip>:5683
```

### 2. Configure the webhook (optional)

Edit `ensemble.tw` and update the `url` in the `webhook-excursion` action to point to your LIMS or alerting endpoint.
If you don't need webhooks, remove or comment out the `webhook-excursion` action block.

### 3. Start Tinkwell

```bash
tw start ensemble.tw
```

If running from a build output directory (not an installed copy), use `./Tinkwell.Coordinator ensemble.tw` instead.

The LwM2M server starts listening on UDP port 5683.
When the chamber registers, Tinkwell automatically subscribes to temperature and humidity resources via LwM2M Observe.

### 4. Verify registration

```bash
tw lwm2m list
```

You should see the chamber listed as a registered client with its endpoint name.

### 5. Monitor

Each watch command is blocking and needs its own terminal.

**Terminal 1** — live sensor values:

```bash
tw measures watch
```

**Terminal 2** — excursion alerts with audible bell:

```bash
tw signals watch --beep
```

**Any terminal** — one-shot queries:

```bash
tw measures list
tw store get compliance/excursion/temp-high
tw events list --last 50
```

### 6. Audit trail

All events (registrations, measure changes, signal firings) are persisted to `chamber-events.db` (SQLite).
This file can be queried directly or exported for validation reports:

```bash
tw events list --output jsonl > audit-export.jsonl
```

## Signal thresholds

### Critical (5-minute hold)

| Signal | Condition | ICH limit |
|--------|-----------|-----------|
| `temp-high` | Temperature > 27°C for 5 min | Above 25°C + 2°C |
| `temp-low` | Temperature < 23°C for 5 min | Below 25°C - 2°C |
| `humidity-high` | Humidity > 65% RH for 5 min | Above 60% + 5% |
| `humidity-low` | Humidity < 55% RH for 5 min | Below 60% - 5% |

### Warning (immediate)

| Signal | Condition | Purpose |
|--------|-----------|---------|
| `temp-drift` | Temperature outside 24–26°C | Early drift detection |
| `humidity-drift` | Humidity outside 57–63% RH | Early drift detection |

## Customization

- **Different ICH conditions** — For accelerated testing (40°C ± 2°C / 75% RH ± 5%), adjust the measure ranges and signal thresholds.
- **Multiple chambers** — Add additional `lwm2m` blocks with different ports, or use the chamber's endpoint name to route to distinct measures.
- **Webhook authentication** — Add `authorization = "Bearer your-token"` to the `do http-post` block.
- **Longer audit retention** — The `record-all-changes` action uses a 7-day TTL in the state store; adjust `ttl` as needed.
  The event persistence database (`chamber-events.db`) retains all events indefinitely.
