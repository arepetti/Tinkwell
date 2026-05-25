"""
tw_do.py - Execute an arbitrary command repeatedly with optional generated values.

Examples:

    # POST a random value to a CoAP endpoint every 0.5s
    python tw_do.py --cadence 0.5 --variation 0:100 -- tw coap send post /sensors/temperature -d $value$

    # Stress test: fire tw measures set as fast as possible (no delay)
    python tw_do.py --variation 18:26 --algorithm noise --cadence 0 -- tw measures set temperature $value$

    # Cadence-only (no value needed): ping the coordinator every 1-3s
    python tw_do.py --cadence 1:3 -- tw ping

    # Random walk, one value per second for 60 seconds
    python tw_do.py --algorithm drift --variation 0:50 --duration 60 -- tw measures set pressure $value$

    # Reproducible sequence with a fixed seed
    python tw_do.py --seed 42 --variation 10:20 -- tw measures set pressure $value$

    # Rapid-fire CoAP PUTs with Gaussian noise
    python tw_do.py --variation 220:240 --algorithm noise --cadence 0.1 -- tw coap send put /device/voltage -d $value$

    # Dry run: see what would be executed without running anything
    python tw_do.py --dry-run --variation 0:100 --cadence 0.5 --duration 5 -- tw measures set temperature $value$

Everything after '--' is the command template. The literal $value$ in any
argument is replaced with the generated value. If $value$ is absent the
command is executed as-is (cadence-only mode).

Requires the target command (e.g. `tw`) on PATH.
"""

import argparse
import math
import random
import subprocess
import sys
import time


VALUE_PLACEHOLDER = "$value$"
ALGORITHMS = ["random", "noise", "drift"]


def parse_range(text: str) -> tuple[float, float]:
    """Parse 'VALUE' as (VALUE, VALUE) or 'MIN:MAX' as (MIN, MAX)."""
    if ":" in text:
        lo, hi = text.split(":", 1)
        return float(lo), float(hi)
    v = float(text)
    return v, v


def resolve_range(variation: str | None, center: float | None) -> tuple[float, float, float]:
    """Return (lo, hi, center) from CLI args."""
    if variation is not None:
        lo, hi = parse_range(variation)
        if lo == hi:
            half = lo
            c = center if center is not None else 50.0
            return c - half, c + half, c
        else:
            c = center if center is not None else (lo + hi) / 2
            return lo, hi, c

    lo, hi = 0.0, 100.0
    c = center if center is not None else (lo + hi) / 2
    return lo, hi, c


def resolve_cadence(text: str) -> tuple[float, float]:
    """Parse cadence as fixed or min:max range."""
    lo, hi = parse_range(text)
    return lo, hi


def clamp(value: float, lo: float, hi: float) -> float:
    return max(lo, min(hi, value))


# ---------------------------------------------------------------------------
# Algorithms
# ---------------------------------------------------------------------------

def algo_random(lo: float, hi: float, _center: float, _prev: float,
                rng: random.Random) -> float:
    return rng.uniform(lo, hi)


def algo_noise(lo: float, hi: float, center: float, _prev: float,
               rng: random.Random) -> float:
    stddev = (hi - lo) / 6
    return clamp(rng.gauss(center, stddev), lo, hi)


def algo_drift(lo: float, hi: float, _center: float, prev: float,
               rng: random.Random) -> float:
    step = (hi - lo) / 50
    return clamp(prev + rng.gauss(0, step), lo, hi)


ALGO_MAP = {
    "random": algo_random,
    "noise": algo_noise,
    "drift": algo_drift,
}


def expand_command(template: list[str], value: float) -> list[str]:
    """Replace $value$ placeholders in the command template."""
    formatted = f"{value:.6g}"
    return [arg.replace(VALUE_PLACEHOLDER, formatted) for arg in template]


def execute(cmd: list[str]) -> bool:
    """Run a command. Returns True on success."""
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, timeout=10)
        return result.returncode == 0
    except (subprocess.TimeoutExpired, OSError):
        return False


def run(args):
    if not args.command:
        print("Error: no command specified after '--'.", file=sys.stderr)
        sys.exit(1)

    lo, hi, center = resolve_range(args.variation, args.center)
    cadence_lo, cadence_hi = resolve_cadence(args.cadence)
    algo_fn = ALGO_MAP[args.algorithm]
    rng = random.Random(args.seed)
    prev = center
    has_placeholder = any(VALUE_PLACEHOLDER in arg for arg in args.command)

    print(f"Command template: {' '.join(args.command)}")
    if has_placeholder:
        print(f"Algorithm: {args.algorithm}  range=[{lo}, {hi}]  center={center}")
    if cadence_lo == 0 and cadence_hi == 0:
        print("Cadence: none (as fast as possible)", end="")
    elif cadence_lo == cadence_hi:
        print(f"Cadence: {cadence_lo}s", end="")
    else:
        print(f"Cadence: {cadence_lo}..{cadence_hi}s", end="")
    if args.duration:
        print(f"  duration: {args.duration}s")
    else:
        print("  (Ctrl+C to stop)")
    if args.dry_run:
        print("[DRY RUN]")

    t0 = time.monotonic()
    count = 0
    failures = 0
    try:
        while True:
            elapsed = time.monotonic() - t0
            if args.duration and elapsed >= args.duration:
                break

            value = algo_fn(lo, hi, center, prev, rng)
            prev = value
            cmd = expand_command(args.command, value)
            ts = time.strftime("%H:%M:%S")

            if args.dry_run:
                label = f"  value={value:.6g}" if has_placeholder else ""
                print(f"  [{ts}]{label}  {' '.join(cmd)}")
            else:
                ok = execute(cmd)
                count += 1
                if not ok:
                    failures += 1
                status = "" if ok else "  [FAILED]"
                label = f"  value={value:.6g}" if has_placeholder else ""
                print(f"  [{ts}]{label}  {' '.join(cmd)}{status}")

            no_delay = cadence_lo == 0 and cadence_hi == 0
            if not no_delay:
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
    if args.dry_run:
        print(f"\nDry run finished after {elapsed:.1f}s.")
    else:
        fail_msg = f" ({failures} failed)" if failures else ""
        print(f"\nStopped after {elapsed:.1f}s, {count} executions{fail_msg}.")


def main():
    parser = argparse.ArgumentParser(
        description="Execute a command repeatedly with optional generated values.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--variation",
                        help="Value range: V for +/-V around center, "
                             "or MIN:MAX for explicit bounds (default: 0:100)")
    parser.add_argument("--center", type=float, default=None,
                        help="Center point for noise/drift (default: midpoint of range)")
    parser.add_argument("--algorithm", choices=ALGORITHMS, default="random",
                        help="Value generation algorithm (default: random)")
    parser.add_argument("--cadence", default="1",
                        help="Interval in seconds: fixed, MIN:MAX for random, "
                             "or 0 for no delay (default: 1)")
    parser.add_argument("--duration", type=float, default=None,
                        help="Stop after N seconds (default: run until Ctrl+C)")
    parser.add_argument("--seed", type=int, default=None,
                        help="RNG seed for reproducibility")
    parser.add_argument("--dry-run", action="store_true",
                        help="Print commands without executing them")
    parser.add_argument("command", nargs=argparse.REMAINDER,
                        help="Command template (after '--'). "
                             "Use $value$ as placeholder for the generated value.")

    args = parser.parse_args()

    if args.command and args.command[0] == "--":
        args.command = args.command[1:]

    run(args)


if __name__ == "__main__":
    main()
