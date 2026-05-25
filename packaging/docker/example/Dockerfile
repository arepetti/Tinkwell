# =============================================================================
# Example: derived Tinkwell image
# =============================================================================
#
# This file is a TEMPLATE you copy into your own repository. It is not
# built or published by the Tinkwell project. Use it when you want an
# immutable, deployable image that already contains your ensemble and
# plugins — the recommended pattern for production.
#
# Repository layout this template assumes:
#
#   .
#   ├── Dockerfile         (this file, customized)
#   ├── ensemble.tw        (your top-level ensemble)
#   ├── include/           (optional: files referenced by `include` in ensemble.tw)
#   │   ├── measures.tw
#   │   └── ...
#   └── plugins/           (optional: third-party plugins to bundle)
#       ├── my-runlet@1.0.0/
#       │   └── My.Runlet.dll
#       └── ...
#
# Build:
#
#   docker build \
#     --build-arg TINKWELL_VERSION=0.8.0 \
#     -t my-org/my-tinkwell:1.0 .
#
# Always pin TINKWELL_VERSION to an exact release. Using `latest` makes your
# builds non-reproducible and exposes you to silent upstream changes.
#
# Run:
#
#   docker run --rm \
#     -p 5683:5683/udp \
#     my-org/my-tinkwell:1.0
#
# Secrets (broker passwords, DB connection strings, TLS certs) should NOT be
# COPYed into the image. Pass them at runtime via environment variables or
# Docker / Kubernetes secrets, and reference them from the ensemble with
# `{{ env.NAME }}` (Liquid preprocessing) or from a mounted file.

ARG TINKWELL_VERSION=latest
FROM ghcr.io/arepetti/tinkwell:${TINKWELL_VERSION}

# Ensemble configuration. `include` paths inside ensemble.tw should be
# either absolute (/etc/tinkwell/include/...) or relative to /etc/tinkwell/.
COPY --chown=tinkwell:tinkwell ensemble.tw /etc/tinkwell/ensemble.tw
COPY --chown=tinkwell:tinkwell include/    /etc/tinkwell/include/

# Plugins. The directory layout must follow the `name@version` convention
# documented in https://github.com/arepetti/Tinkwell/blob/main/docs/reference/plugins.md.
# The base image already sets TINKWELL_PLUGIN_PATH=/var/lib/tinkwell/plugins,
# so anything dropped here is picked up automatically.
COPY --chown=tinkwell:tinkwell plugins/    /var/lib/tinkwell/plugins/

# Healthcheck, entrypoint, user, and exposed surface are inherited from the
# base image. There is no need to override them unless you have specific
# requirements.
