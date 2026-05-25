"""
tw_simulate_measure.py - Simulate a Tinkwell measure by writing values with tw measures set.

Examples:

    # Random values across the measure's range (or 0..100 default range
    python tw_simulate_measure.py temperature

    # Explicit range (18 to 26)
    python tw_simulate_measure.py temperature --variation 18:26

    # Small variation (+/- 2) around the measure's current value
    python tw_simulate_measure.py temperature --variation 2

    # Variation around a specific center point (+/- 3 around 22)
    python tw_simulate_measure.py temperature --variation 3 --center 22

    # Gaussian noise centered at 22, within 18..26
    python tw_simulate_measure.py temperature --variation 18:26 --center 22 --algorithm noise

    # Random walk (drift) with a fixed seed for reproducibility
    python tw_simulate_measure.py pressure --algorithm drift --seed 42

    # Sine wave with a 120-second period
    python tw_simulate_measure.py voltage --variation 220:240 --algorithm sine --period 120

    # Triangle wave, custom cadence
    python tw_simulate_measure.py current --variation 0:5 --algorithm triangle --cadence 0.5

    # Square wave alternating between 0 and 1 every 30 seconds
    python tw_simulate_measure.py relay-state --variation 0:1 --algorithm square --period 60

    # Random cadence between 0.2 and 3 seconds
    python tw_simulate_measure.py humidity --algorithm noise --cadence 0.2:3

    # Run for exactly 60 seconds then stop
    python tw_simulate_measure.py temperature --duration 60

    # Combine: sine wave, random cadence, limited duration
    python tw_simulate_measure.py temperature --variation 15:35 --algorithm sine --period 90 --cadence 0.5:2 --duration 300

    # Negative range (use = syntax to avoid argparse ambiguity)
    python tw_simulate_measure.py offset --variation=-10:10 --algorithm noise

Requires `tw` on PATH and a running Tinkwell instance.
"""

import argparse
import json
import math
import random
import subprocess
import sys
import time


ALGORITHMS = ["random", "noise", "drift", "sine", "triangle", "square"]


def parse_range(text: str) -> tuple[float, float]:
    """Parse 'VALUE' as (VALUE, VALUE) or 'MIN:MAX' as (MIN, MAX)."""
    if ":" in text:
        lo, hi = text.split(":", 1)
        return float(lo), float(hi)
    v = float(text)
    return v, v


def query_measure(name: str) -> dict | None:
    """Best-effort query of a measure's current definition via tw measures get."""
    try:
        result = subprocess.run(
            ["tw", "measures", "get", name, "--format", "jsonl"],
            capture_output=True, text=True, timeout=5,
        )
        if result.returncode == 0 and result.stdout.strip():
            return json.loads(result.stdout.strip().splitlines()[0])
    except (subprocess.TimeoutExpired, json.JSONDecodeError, OSError):
        pass
    return None


def resolve_range(args, measure_info: dict | None) -> tuple[float, float, float]:
    """Return (lo, hi, center) from CLI args and optional measure metadata."""
    if args.variation is not None:
        lo, hi = parse_range(args.variation)
        if lo == hi:
            half = lo
            if args.center is not None:
                center = args.center
            elif measure_info and measure_info.get("value") is not None:
                center = float(measure_info["value"])
            else:
                center = 50.0
            return center - half, center + half, center
        else:
            center = args.center if args.center is not None else (lo + hi) / 2
            return lo, hi, center

    lo, hi = 0.0, 100.0
    if measure_info:
        if measure_info.get("min") is not None:
            lo = float(measure_info["min"])
        if measure_info.get("max") is not None:
            hi = float(measure_info["max"])
    center = args.center if args.center is not None else (lo + hi) / 2
    return lo, hi, center


def resolve_cadence(text: str) -> tuple[float, float]:
    """Parse cadence as fixed or min:max range."""
    lo, hi = parse_range(text)
    if lo == hi:
        return lo, lo
    return lo, hi


def clamp(value: float, lo: float, hi: float) -> float:
    return max(lo, min(hi, value))


# ---------------------------------------------------------------------------
# Algorithms -- each returns the next value given the current state.
# ---------------------------------------------------------------------------

def algo_random(lo: float, hi: float, _center: float, _t: float,
                _period: float, _prev: float, rng: random.Random) -> float:
    return rng.uniform(lo, hi)


def algo_noise(lo: float, hi: float, center: float, _t: float,
               _period: float, _prev: float, rng: random.Random) -> float:
    stddev = (hi - lo) / 6
    return clamp(rng.gauss(center, stddev), lo, hi)


def algo_drift(lo: float, hi: float, _center: float, _t: float,
               _period: float, prev: float, rng: random.Random) -> float:
    step = (hi - lo) / 50
    return clamp(prev + rng.gauss(0, step), lo, hi)


def algo_sine(lo: float, hi: float, center: float, t: float,
              period: float, _prev: float, _rng: random.Random) -> float:
    amplitude = (hi - lo) / 2
    return clamp(center + amplitude * math.sin(2 * math.pi * t / period), lo, hi)


def algo_triangle(lo: float, hi: float, _center: float, t: float,
                  period: float, _prev: float, _rng: random.Random) -> float:
    phase = (t % period) / period
    if phase < 0.5:
        return lo + (hi - lo) * (phase * 2)
    return hi - (hi - lo) * ((phase - 0.5) * 2)


def algo_square(lo: float, hi: float, _center: float, t: float,
                period: float, _prev: float, _rng: random.Random) -> float:
    phase = (t % period) / period
    return lo if phase < 0.5 else hi


ALGO_MAP = {
    "random": algo_random,
    "noise": algo_noise,
    "drift": algo_drift,
    "sine": algo_sine,
    "triangle": algo_triangle,
    "square": algo_square,
}


def write_value(name: str, value: float) -> bool:
    """Write a value via tw measures set. Returns True on success."""
    try:
        result = subprocess.run(
            ["tw", "measures", "set", name, str(value)],
            capture_output=True, text=True, timeout=5,
        )
        return result.returncode == 0
    except (subprocess.TimeoutExpired, OSError):
        return False


def run(args):
    measure_info = query_measure(args.measure)
    lo, hi, center = resolve_range(args, measure_info)
    cadence_lo, cadence_hi = resolve_cadence(args.cadence)
    algo_fn = ALGO_MAP[args.algorithm]
    rng = random.Random(args.seed)
    prev = center

    print(f"Simulating {args.measure}  algorithm={args.algorithm}  "
          f"range=[{lo}, {hi}]  center={center}")
    if cadence_lo == cadence_hi:
        print(f"Cadence: {cadence_lo}s", end="")
    else:
        print(f"Cadence: {cadence_lo}..{cadence_hi}s", end="")
    if args.duration:
        print(f"  duration: {args.duration}s")
    else:
        print("  (Ctrl+C to stop)")

    t0 = time.monotonic()
    try:
        while True:
            elapsed = time.monotonic() - t0
            if args.duration and elapsed >= args.duration:
                break

            value = algo_fn(lo, hi, center, elapsed, args.period, prev, rng)
            prev = value

            ok = write_value(args.measure, value)
            ts = time.strftime("%H:%M:%S")
            status = "" if ok else "  [FAILED]"
            print(f"  [{ts}] {args.measure} = {value:.6g}{status}")

            delay = rng.uniform(cadence_lo, cadence_hi) if cadence_lo != cadence_hi else cadence_lo
            if args.duration:
                remaining = args.duration - (time.monotonic() - t0)
                if remaining <= 0:
                    break
                delay = min(delay, remaining)
            time.sleep(delay)
    except KeyboardInterrupt:
        pass

    elapsed = time.monotonic() - t0
    print(f"\nStopped after {elapsed:.1f}s.")


def main():
    parser = argparse.ArgumentParser(
        description="Simulate a Tinkwell measure by writing values with tw measures set.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("measure", help="Measure name")
    parser.add_argument("--variation",
                        help="Range: a single value V for +/-V around center, "
                             "or MIN:MAX for an explicit range (default: 0:100)")
    parser.add_argument("--center", type=float, default=None,
                        help="Center point for noise/drift/sine (default: midpoint of range)")
    parser.add_argument("--algorithm", choices=ALGORITHMS, default="random",
                        help="Simulation algorithm (default: random)")
    parser.add_argument("--cadence", default="1",
                        help="Interval in seconds: fixed value or MIN:MAX for random (default: 1)")
    parser.add_argument("--period", type=float, default=60,
                        help="Cycle length in seconds for sine/triangle/square (default: 60)")
    parser.add_argument("--duration", type=float, default=None,
                        help="Stop after N seconds (default: run until Ctrl+C)")
    parser.add_argument("--seed", type=int, default=None,
                        help="RNG seed for reproducibility")
    run(parser.parse_args())


if __name__ == "__main__":
    main()
