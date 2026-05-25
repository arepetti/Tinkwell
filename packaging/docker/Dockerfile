# syntax=docker/dockerfile:1.7
#
# Tinkwell base runtime image.
#
# Builds a self-contained Linux publish of Tinkwell (coordinator, runners,
# built-in runlets, and the `tw` CLI) on top of `mcr.microsoft.com/dotnet/
# runtime-deps:10.0-bookworm-slim`. The published image is a runtime only:
# it does NOT contain an `ensemble.tw` or any plugins. Those are supplied
# by the user, either:
#
#   - by bind-mounting them into the container (development), or
#   - by building a derived image with `FROM ghcr.io/arepetti/tinkwell:<ver>`
#     and `COPY`ing the ensemble + plugins (production).
#
# See docs/getting-started/docker.md for the full story.
#
# Build (multi-arch via buildx):
#
#   docker buildx build \
#       --platform linux/amd64,linux/arm64 \
#       --build-arg VERSION=0.8.0 \
#       -f packaging/docker/Dockerfile \
#       -t ghcr.io/arepetti/tinkwell:0.8.0 .
#
# Single-arch local build (from repo root):
#
#   docker build \
#       --build-arg VERSION=0.8.0-dev \
#       -f packaging/docker/Dockerfile \
#       -t tinkwell:dev .

ARG DOTNET_VERSION=10.0
ARG DEBIAN_RELEASE=bookworm-slim

# ----------------------------------------------------------------------------
# Stage 1: build
# ----------------------------------------------------------------------------
# Produces a self-contained publish of the whole solution for the requested
# target architecture. `TARGETARCH` is set automatically by `docker buildx`
# (one of `amd64` or `arm64`); we map it to the corresponding .NET RID.

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-${DEBIAN_RELEASE} AS build

ARG TARGETARCH
ARG VERSION=0.0.0-dev

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    NUGET_XMLDOC_MODE=skip

WORKDIR /src

COPY src/ ./src/

WORKDIR /src/src

RUN case "${TARGETARCH}" in \
        amd64) RID=linux-x64 ;; \
        arm64) RID=linux-arm64 ;; \
        *) echo "Unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac && \
    echo "Publishing Tinkwell ${VERSION} for ${RID}" && \
    dotnet publish Tinkwell.slnx \
        -c Release \
        -r "${RID}" \
        --self-contained \
        -p:PublishSingleFile=false \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:VersionPrefix="${VERSION}" \
        -p:ContinuousIntegrationBuild=true

# `src/app/Directory.Build.props` sets `OutDir` to `artifacts/Release/` (flat),
# so all published files for every app project land in a single directory.

# ----------------------------------------------------------------------------
# Stage 2: runtime
# ----------------------------------------------------------------------------
# Mirrors the layout produced by the .deb package (see packaging/deb/build-deb.sh):
#
#   /usr/lib/tinkwell/    -- the self-contained publish output
#   /usr/bin/tw           -- symlink to /usr/lib/tinkwell/tw
#
# Plus container-specific bits:
#
#   /etc/tinkwell/                       -- user-supplied ensemble.tw (volume)
#   /var/lib/tinkwell/                   -- runtime state, named volume
#   /var/lib/tinkwell/plugins/           -- plugin drop directory
#   /usr/local/bin/tinkwell-entrypoint.sh -- entrypoint script

FROM mcr.microsoft.com/dotnet/runtime-deps:${DOTNET_VERSION}-${DEBIAN_RELEASE} AS runtime

ARG VERSION=0.0.0-dev

LABEL org.opencontainers.image.title="Tinkwell" \
      org.opencontainers.image.description="Configuration-driven IoT and lab automation runtime. Runs the Tinkwell coordinator and its runners. Bring your own ensemble.tw." \
      org.opencontainers.image.source="https://github.com/arepetti/Tinkwell" \
      org.opencontainers.image.documentation="https://arepetti.github.io/Tinkwell/getting-started/docker.html" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.vendor="Adriano Repetti"

ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    TINKWELL_CONFIG=/etc/tinkwell/ensemble.tw \
    TINKWELL_PLUGIN_PATH=/var/lib/tinkwell/plugins \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Install runtime extras:
#   tini    -- minimal init; reaps zombie runner processes the coordinator
#              spawns and forwards signals cleanly to PID 1.
#   ca-certificates -- needed for HTTPS egress (e.g. OTLP, http-post actions,
#                      MQTT over TLS).
RUN apt-get update \
    && apt-get install -y --no-install-recommends tini ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Non-root service user, mirroring the systemd unit's `tinkwell` account.
# UID/GID 1000 keeps bind-mounted ensembles/plugins readable when the host
# directory is owned by the first regular user.
RUN groupadd --system --gid 1000 tinkwell \
    && useradd  --system --uid 1000 --gid tinkwell \
                --home-dir /var/lib/tinkwell --shell /usr/sbin/nologin tinkwell \
    && mkdir -p /usr/lib/tinkwell \
                /etc/tinkwell \
                /var/lib/tinkwell \
                /var/lib/tinkwell/plugins \
    && chown -R tinkwell:tinkwell /etc/tinkwell /var/lib/tinkwell

COPY --from=build --chown=root:root /src/artifacts/Release/ /usr/lib/tinkwell/

RUN chmod +x /usr/lib/tinkwell/tw \
             /usr/lib/tinkwell/Tinkwell.Coordinator \
             /usr/lib/tinkwell/Tinkwell.Runner.Grpc \
             /usr/lib/tinkwell/Tinkwell.Runner.Headless \
    && ln -sf /usr/lib/tinkwell/tw /usr/bin/tw

COPY --chown=root:root packaging/docker/entrypoint.sh /usr/local/bin/tinkwell-entrypoint.sh
RUN chmod 0755 /usr/local/bin/tinkwell-entrypoint.sh

VOLUME ["/etc/tinkwell", "/var/lib/tinkwell"]

WORKDIR /var/lib/tinkwell
USER tinkwell

# Health is verified by talking to the coordinator over the local named pipe.
# `tw ping` exits 0 when reachable, non-zero otherwise; safe inside the same
# container where pipe namespace is shared.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD ["/usr/bin/tw", "ping"]

# tini -g forwards SIGTERM to the whole process group, so the coordinator's
# graceful-shutdown path runs (same semantics as `tw quit`).
ENTRYPOINT ["/usr/bin/tini", "-g", "--", "/usr/local/bin/tinkwell-entrypoint.sh"]

# No CMD: the entrypoint exec's `tw start "$TINKWELL_CONFIG" "$@"`, so any
# extra arguments passed to `docker run` flow through to `tw start`.
