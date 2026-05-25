# Scripts

Utility scripts for documentation generation, ad-hoc command execution,
and measure simulation.
They are not part of the build and are not installed with Tinkwell —
run them directly from this folder when you need them.

All scripts target Python 3.10+ (for the modern type hints they use).
Run them from the repository root or from this folder; nothing here
expects a particular working directory.

## Prerequisites

| Tool | Used by |
|------|---------|
| Python 3.10+ | all scripts |
| [`requests`](https://pypi.org/project/requests/) (`pip install requests`) | `fetch-doc-units.py` |
| Tinkwell `tw` CLI on `PATH` | `tw_do.py`, `tw_simulate_measure.py` |
| A running Tinkwell instance | `tw_simulate_measure.py`, and `tw_do.py` when the wrapped command needs one |

## `fetch-doc-units.py`

**Purpose.** Regenerate `Units.md` (the supported units of measurement
reference) from the upstream
[UnitsNet](https://github.com/angularsen/UnitsNet) JSON definitions.
It downloads `UnitEnumValues.g.json` and the per-quantity definition
files, groups quantities into logical categories, and emits a single
Markdown document with a table of contents, descriptions, and unit
abbreviations.

**Synopsis.**

```bash
pip install requests
python scripts/fetch-doc-units.py
```

**Output.** `Units.json` (raw data, kept for inspection) and `Units.md`,
both written to the current working directory. Move/commit `Units.md`
into `docs/reference/` after reviewing the diff.

**When to run.** When UnitsNet ships new quantities or units, or when
the curated `quantity_groups` mapping at the top of the script is
updated to reclassify entries.

## `tw_do.py`

**Purpose.** Repeatedly run an arbitrary command, optionally substituting
a generated numeric value into its arguments. The thin wrapper around
`subprocess.run` makes it useful for load-testing, smoke-testing, and
quick "what happens when this fires N times a second?" experiments —
against `tw`, against any HTTP/CoAP/MQTT client, or against any other
CLI you have installed.

**Synopsis.**

```bash
python scripts/tw_do.py [--variation V|MIN:MAX] [--center C]
                        [--algorithm random|noise|drift]
                        [--cadence S|MIN:MAX|0] [--duration SEC]
                        [--seed N] [--dry-run]
                        -- <command...>
```

Everything after `--` is the command template. The literal `$value$` in
any argument is replaced with the generated value before execution; if
no argument contains `$value$` the command runs as-is (cadence-only
mode).

**Key arguments.**

- **`--variation`**: `V` for `±V` around the center, or `MIN:MAX` for
  explicit bounds. Default `0:100`.
- **`--algorithm`**: `random` (uniform), `noise` (Gaussian around the
  center), or `drift` (random walk). Default `random`.
- **`--cadence`**: seconds between iterations. Fixed (`0.5`), random
  range (`1:3`), or `0` for "as fast as possible". Default `1`.
- **`--duration`**: stop after N seconds. Default: run until `Ctrl+C`.
- **`--seed`**: RNG seed for reproducible sequences.
- **`--dry-run`**: print the commands without running them.

**Examples.**

```bash
# POST a random value to a CoAP endpoint every 0.5s
python scripts/tw_do.py --cadence 0.5 --variation 0:100 \
  -- tw coap send post /sensors/temperature -d $value$

# Random walk, one value per second for 60 seconds
python scripts/tw_do.py --algorithm drift --variation 0:50 --duration 60 \
  -- tw measures set pressure $value$

# Cadence-only (no value): ping the coordinator every 1-3s
python scripts/tw_do.py --cadence 1:3 -- tw ping

# Dry run — see what would be executed, without running anything
python scripts/tw_do.py --dry-run --variation 0:100 --cadence 0.5 \
  --duration 5 -- tw measures set temperature $value$
```

**Quick load-testing recipe.** To stress one or more endpoints, set
`--cadence 0` (no delay between iterations) and start several copies
of the script in parallel — each shell window is one client. Pin
`--seed` to make individual runs reproducible, and use `--duration` to
keep the experiment bounded:

```bash
# Terminal 1: hammer the measure store as fast as possible for 30s
python scripts/tw_do.py --cadence 0 --duration 30 --variation 18:26 \
  --algorithm noise -- tw measures set temperature $value$

# Terminal 2: in parallel, hit a different measure
python scripts/tw_do.py --cadence 0 --duration 30 --variation 0:1 \
  -- tw measures set relay-state $value$
```

The script prints a summary line on exit (count and number of failures)
so each window gives you a quick throughput/error figure.

## `tw_simulate_measure.py`

**Purpose.** Drive a single Tinkwell measure with a synthetic signal by
repeatedly calling `tw measures set`. Useful when you want a measure to
"look alive" while exercising downstream consumers (events, history,
dashboards, state machines) without wiring real sensors.

Unlike `tw_do.py`, this script understands measures: it queries
`tw measures get` first and uses the measure's current value, `min`,
and `max` as defaults when `--variation` / `--center` are omitted.

**Synopsis.**

```bash
python scripts/tw_simulate_measure.py <measure>
       [--variation V|MIN:MAX] [--center C]
       [--algorithm random|noise|drift|sine|triangle|square]
       [--cadence S|MIN:MAX] [--period SEC]
       [--duration SEC] [--seed N]
```

**Algorithms.** In addition to the three from `tw_do.py`, this script
also supports `sine`, `triangle`, and `square` waveforms (controlled by
`--period`).

**Examples.**

```bash
# Random values across the measure's own range
python scripts/tw_simulate_measure.py temperature

# Gaussian noise centered at 22, within 18..26
python scripts/tw_simulate_measure.py temperature \
  --variation 18:26 --center 22 --algorithm noise

# Sine wave with a 120-second period
python scripts/tw_simulate_measure.py voltage \
  --variation 220:240 --algorithm sine --period 120

# Run for exactly 60 seconds then stop
python scripts/tw_simulate_measure.py temperature --duration 60

# Negative range — use '=' to avoid argparse ambiguity
python scripts/tw_simulate_measure.py offset --variation=-10:10 --algorithm noise
```

## Common patterns

**Reproducible runs.** Pass `--seed` to both `tw_do.py` and
`tw_simulate_measure.py` to get the same value sequence on every
invocation — handy when comparing two builds, two configurations, or
two branches.

**Dry-run first.** `tw_do.py --dry-run` prints the exact commands it
would execute. Use it to validate placeholder substitution and quoting
before pointing the script at a live system.

**Bounded experiments.** Always pair load-testing or long-running
simulation runs with `--duration`; otherwise the script runs until
`Ctrl+C` and is easy to forget.
