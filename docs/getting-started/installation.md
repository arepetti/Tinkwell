# Installation

## Windows

### winget (recommended)

Once the package is published to the [winget community repository](https://github.com/microsoft/winget-pkgs):

```powershell
winget install AdrianoRepetti.Tinkwell
```

This installs `tw.exe` and adds it to your PATH.
Both x64 and ARM64 are available; winget picks the right one for your machine.
Verify with:

```powershell
tw --version
```

### Manual (portable ZIP)

1. Download the ZIP for your architecture from the [latest GitHub release](https://github.com/arepetti/Tinkwell/releases/latest):
   - `tinkwell-<version>-win-x64.zip` for Intel/AMD
   - `tinkwell-<version>-win-arm64.zip` for ARM64 (Surface Pro, Snapdragon laptops)
2. Extract to a directory of your choice (e.g. `C:\Tools\tinkwell`).
3. Add that directory to your `PATH` environment variable.
4. Open a new terminal and run:

```powershell
tw --version
```

## Linux (Debian / Ubuntu)

### .deb package

1. Download the `.deb` for your architecture from the [latest GitHub release](https://github.com/arepetti/Tinkwell/releases/latest):
   - `tinkwell_<version>_amd64.deb` for x86-64
   - `tinkwell_<version>_arm64.deb` for ARM64 (Raspberry Pi 4/5, etc.)
2. Install with `dpkg`:

```bash
sudo dpkg -i tinkwell_*.deb
```

The package installs binaries under `/usr/lib/tinkwell/` and symlinks `tw` into `/usr/bin/`, so it is immediately available on PATH:

```bash
tw --version
```

The `.deb` also installs a man page generated from the [CLI reference](../user-guide/cli.md):

```bash
man tw
```

To run Tinkwell as a managed service that starts on boot, restarts on failure, and logs to `journald`, see [Running under systemd](systemd.md).

To uninstall:

```bash
sudo dpkg -r tinkwell
```

### Manual (tarball)

1. Download the tarball for your architecture from the [latest GitHub release](https://github.com/arepetti/Tinkwell/releases/latest):
   - `tinkwell-<version>-linux-x64.tar.gz`
   - `tinkwell-<version>-linux-arm64.tar.gz`
2. Extract and install:

```bash
sudo mkdir -p /usr/lib/tinkwell
sudo tar xzf tinkwell-*-linux-*.tar.gz -C /usr/lib/tinkwell
sudo ln -sf /usr/lib/tinkwell/tw /usr/bin/tw
```

3. Verify:

```bash
tw --version
```

## Docker

A multi-architecture image is published on every release at `ghcr.io/arepetti/tinkwell` (Linux `amd64` and `arm64`). The image is a runtime only — it expects you to provide your own `ensemble.tw`:

```bash
docker run --rm \
  -v "$PWD/ensemble.tw":/etc/tinkwell/ensemble.tw:ro \
  -p 5683:5683/udp \
  ghcr.io/arepetti/tinkwell:latest
```

See [Running under Docker](docker.md) for the full walkthrough, including the bind-mount and derived-image patterns, port matrix, volumes, healthcheck behavior, telemetry, and troubleshooting.

## Quick start

After installation, the typical workflow is:

```bash
mkdir my-project
cd my-project
# optional: tw plugin search   # list packages from the registry, if configured
# tw plugin install <path-or-url-to.zip>   # see plugins guide
edit ensemble.tw
tw start
```

See the [CLI reference](../user-guide/cli.md) for the full command list and the [plugins guide](../reference/plugins.md) for details on discovering and installing plugins.
