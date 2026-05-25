# Running Tinkwell under systemd

The Tinkwell `.deb` package installs the `tw` CLI and runtime but does not register a service. Running the coordinator under `systemd` is recommended for production deployments on a server, a Raspberry Pi, or any unattended Linux host: you get automatic startup on boot, restart on crash, and centralized logging through `journald`.

This page is a copy-pasteable walkthrough. It assumes Tinkwell is already installed via the [Debian package](installation.md).

## 1. Create a dedicated system user

Run the coordinator under an unprivileged service account rather than `root`.

```bash
sudo useradd --system \
    --home /var/lib/tinkwell \
    --shell /usr/sbin/nologin \
    tinkwell
sudo mkdir -p /var/lib/tinkwell /etc/tinkwell
sudo chown -R tinkwell:tinkwell /var/lib/tinkwell
```

`/var/lib/tinkwell` is the service's home directory; the coordinator stores runtime state and plugins under it. `/etc/tinkwell` holds the ensemble configuration.

## 2. Place the ensemble configuration

```bash
sudo cp ensemble.tw /etc/tinkwell/ensemble.tw
sudo chown tinkwell:tinkwell /etc/tinkwell/ensemble.tw
```

Any `include` paths inside `ensemble.tw` should be absolute or relative to `/etc/tinkwell/` so they resolve regardless of the working directory.

## 3. Create the unit file

Create `/etc/systemd/system/tinkwell.service`:

```ini
[Unit]
Description=Tinkwell coordinator
Documentation=man:tw(1)
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=tinkwell
Group=tinkwell
WorkingDirectory=/var/lib/tinkwell
ExecStart=/usr/bin/tw start /etc/tinkwell/ensemble.tw
Restart=on-failure
RestartSec=5s
Environment=DOTNET_NOLOGO=1

[Install]
WantedBy=multi-user.target
```

`tw start` runs the coordinator in the foreground, which is what `Type=simple` expects. Do **not** pass `-B` / `--background` in the unit; that would detach and systemd would treat the service as exited immediately.

## 4. Enable and start the service

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now tinkwell
```

Verify it's running:

```bash
sudo systemctl status tinkwell
tw ping
tw status
```

If `tw ping` fails from your interactive shell while the service is running, see [Named-pipe visibility](#named-pipe-visibility) below.

## 5. View logs

The coordinator writes to stdout/stderr, which `systemd` captures in `journald`:

```bash
# Follow live
sudo journalctl -u tinkwell -f

# Last 200 lines
sudo journalctl -u tinkwell -n 200

# Since the last boot
sudo journalctl -u tinkwell -b
```

## 6. Update the configuration

After editing `/etc/tinkwell/ensemble.tw`:

```bash
sudo systemctl restart tinkwell
```

## Optional: hardening

The unit above is intentionally minimal. The following directives add defense-in-depth and are safe for most deployments:

```ini
[Service]
NoNewPrivileges=true
ProtectSystem=full
ProtectHome=true
ReadWritePaths=/var/lib/tinkwell /etc/tinkwell
LockPersonality=true
RestrictRealtime=true
```

### Named-pipe visibility

`tw` uses .NET named pipes to talk to a running coordinator. On Linux, these live under `/tmp/CoreFxPipe_<pipe-name>`. If you add `PrivateTmp=true` to the unit, the service gets a private `/tmp` namespace and the pipe will not be visible to your interactive shell - `tw ping`, `tw status`, etc. will fail with a "coordinator not reachable" error.

If you need both `PrivateTmp` isolation and interactive CLI access, either:

- run the CLI as the `tinkwell` user (`sudo -u tinkwell tw status`), or
- override the pipe name via `--pipe` on both sides and use a pipe path that is reachable from both namespaces.

For most single-purpose hosts, omitting `PrivateTmp` is the pragmatic choice.

## Uninstalling

```bash
sudo systemctl disable --now tinkwell
sudo rm /etc/systemd/system/tinkwell.service
sudo systemctl daemon-reload
sudo userdel tinkwell           # keeps /var/lib/tinkwell intact
sudo rm -rf /var/lib/tinkwell   # only if you also want to wipe state
```

Uninstalling the `.deb` (`sudo dpkg -r tinkwell`) does not remove the service file, the user, or `/var/lib/tinkwell` - they were created here, by hand.
