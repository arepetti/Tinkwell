"""
tw_watch.py - Watch Tinkwell measures and events from Python.

Usage:
    python tw_watch.py                      # watch all measures
    python tw_watch.py --events             # watch events instead
    python tw_watch.py -f sensor            # measures starting with "sensor"
    python tw_watch.py --events -f signals  # events from source "signals"

Requires `tw` on PATH and a running Tinkwell instance.
"""

import argparse
import json
import subprocess
import sys


def watch_stream(cmd: list[str]):
    """Run a tw command and yield parsed JSONL objects."""
    proc = subprocess.Popen(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=1,
    )
    try:
        for line in proc.stdout:
            line = line.strip()
            if not line:
                continue
            try:
                yield json.loads(line)
            except json.JSONDecodeError:
                print(f"[skip] {line}", file=sys.stderr)
    except KeyboardInterrupt:
        pass
    finally:
        proc.terminate()
        proc.wait()


def watch_measures(filter_prefix: str | None = None):
    cmd = ["tw", "measures", "watch", "--format", "jsonl"]
    label = f" ({filter_prefix}*)" if filter_prefix else ""
    print(f"Watching measures{label}  (Ctrl+C to stop)")

    for obj in watch_stream(cmd):
        name = obj.get("name", "?")
        if filter_prefix and not name.startswith(filter_prefix):
            continue
        value = obj.get("value", "?")
        print(f"  {name} = {value}")


def watch_events(filter_source: str | None = None):
    cmd = ["tw", "events", "watch", "--format", "jsonl"]
    if filter_source:
        cmd += ["-s", filter_source]
    label = f" (source={filter_source})" if filter_source else ""
    print(f"Watching events{label}  (Ctrl+C to stop)")

    for obj in watch_stream(cmd):
        verb = obj.get("verb", "?")
        name = obj.get("name", "?")
        src = obj.get("source", "?")
        event_obj = obj.get("object", "")
        payload = obj.get("payload")
        extra = f" {payload}" if payload else ""
        print(f"  [{src}] {verb}: {name} {event_obj}{extra}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Watch Tinkwell measures or events")
    parser.add_argument("--events", action="store_true",
                        help="Watch events instead of measures")
    parser.add_argument("-f", "--filter",
                        help="Name prefix for measures, source for events")
    args = parser.parse_args()

    try:
        if args.events:
            watch_events(args.filter)
        else:
            watch_measures(args.filter)
    except KeyboardInterrupt:
        print("\nStopped.")
