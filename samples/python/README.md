# Python integration example

A minimal script that watches Tinkwell measures or events by streaming JSONL output from the `tw` CLI.
No dependencies beyond Python 3.10+ stdlib.

## Usage

```bash
python tw_watch.py                      # watch all measures
python tw_watch.py -f temp              # only measures starting with "temp"
python tw_watch.py --events             # watch all events
python tw_watch.py --events -f signals  # only signal events
```

Requires `tw` on PATH and a running Tinkwell instance.
Press Ctrl+C to stop.

## How it works

The script spawns `tw measures watch --format jsonl` (or `tw events watch`) as a subprocess and parses each line as JSON.
The `watch_stream()` generator handles subprocess lifecycle and can be reused for any streaming `tw` command.
