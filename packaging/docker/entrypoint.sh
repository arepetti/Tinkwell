#!/bin/sh
#
# Tinkwell container entrypoint.
#
# The released image is a runtime only — it does not contain an ensemble
# configuration. This script checks that the user has provided one (either
# via a bind mount or by deriving from the base image), prints a helpful
# error if they haven't, and otherwise execs `tw start` so that signals
# reach the coordinator and graceful shutdown works correctly.
#
# The ensemble path can be overridden with the TINKWELL_CONFIG environment
# variable; everything else (plugin path, pipe name, etc.) is configured
# through Tinkwell's normal channels.

set -eu

CONFIG="${TINKWELL_CONFIG:-/etc/tinkwell/ensemble.tw}"

if [ ! -f "$CONFIG" ]; then
    cat >&2 <<EOF
[tinkwell] No ensemble configuration found at: $CONFIG

The Tinkwell image is a runtime only. You must provide your own
ensemble.tw, either by:

  1. Bind-mounting it into the container (development):

       docker run --rm \\
         -v "\$PWD/ensemble.tw":/etc/tinkwell/ensemble.tw:ro \\
         -p 5683:5683/udp \\
         ghcr.io/arepetti/tinkwell:latest

  2. Building a derived image (production):

       FROM ghcr.io/arepetti/tinkwell:<version>
       COPY ensemble.tw /etc/tinkwell/ensemble.tw
       COPY plugins/    /var/lib/tinkwell/plugins/

To use a path other than $CONFIG, set the TINKWELL_CONFIG environment
variable when starting the container.

See: https://github.com/arepetti/Tinkwell/blob/main/docs/getting-started/docker.md
EOF
    exit 78    # EX_CONFIG: configuration error (sysexits.h)
fi

exec /usr/bin/tw start "$CONFIG" "$@"
