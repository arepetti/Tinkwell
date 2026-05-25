# Example: derived Tinkwell image

This directory is a **template** you can copy into your own repository when you want to ship Tinkwell as an immutable container that already contains your ensemble configuration and any plugins. It is the recommended pattern for production deployments. See [Running Tinkwell under Docker](../../../docs/getting-started/docker.md) for the full background.

The Tinkwell project does **not** build or publish this image. You build your own from the base image (`ghcr.io/arepetti/tinkwell`).

## Layout

Copy the [`Dockerfile`](Dockerfile) into a new repository alongside your ensemble configuration:

```text
my-tinkwell-deployment/
├── Dockerfile            (copied from this directory, customized)
├── ensemble.tw           (your top-level ensemble)
├── include/              (optional: files referenced by `include` in ensemble.tw)
│   ├── measures.tw
│   ├── signals.tw
│   └── actions.tw
└── plugins/              (optional: third-party plugins to bundle)
    ├── my-runlet@1.0.0/
    │   └── My.Runlet.dll
    └── another-plugin@2.3.1/
        └── Another.Plugin.dll
```

## Build

Pin the base image tag to an exact release (avoid `latest` in production):

```bash
docker build \
  --build-arg TINKWELL_VERSION=0.8.0 \
  -t my-org/my-tinkwell:1.0 .
```

## Run

The base image already declares the entrypoint, healthcheck, user, and volumes. You only need to publish the protocol ports your ensemble uses:

```bash
docker run --rm \
  --name tinkwell \
  -p 5683:5683/udp \
  my-org/my-tinkwell:1.0
```

Inspect the running instance with the CLI inside the container:

```bash
docker exec -it tinkwell tw status
docker exec -it tinkwell tw measures list
```

## Secrets

Do **not** `COPY` secrets into the image. Pass them at runtime via environment variables or Docker / Kubernetes secrets, and reference them from your ensemble with Liquid preprocessing:

```text
mqtt prod-broker {
    host = "{{ env.MQTT_HOST }}"
    username = "{{ env.MQTT_USER }}"
    password = "{{ env.MQTT_PASS }}"
}
```

```bash
docker run --rm \
  -e MQTT_HOST=broker.example.com \
  -e MQTT_USER=ingest \
  -e MQTT_PASS="$(cat ./secrets/mqtt-pass)" \
  -p 5683:5683/udp \
  my-org/my-tinkwell:1.0
```

For more advanced patterns (volume mounts, derived overlays, compose stacks, plugins, ports, environment variables, troubleshooting), see [docs/getting-started/docker.md](../../../docs/getting-started/docker.md).
