# Troubleshooting

Common issues, symptoms, and fixes.

---

## `.tw` syntax errors

**Symptom:** Coordinator fails to start with `ConfigurationSyntaxException` or a message like `Unexpected token at line N`.

**Causes:**
- Missing closing brace `}` on a block.
- Unquoted string containing spaces or special characters — wrap in `"..."`.
- Using `=` inside an expression — expressions use `==` for equality.
- Mismatched parentheses in expressions.

**Fix:** Check the line number reported in the error.
The parser reports the position where it failed, which is usually just past the actual mistake.

---

## Runner fails to start

**Symptom:** Log shows `Runner 'xyz' failed to report ready` or the coordinator retries and eventually gives up.

**Causes:**
- Assembly not found — the `from "..."` path is wrong or the DLL is missing from the `artifacts/` directory.
  Check the `from` value against the actual file on disk.
- Port conflict — another process is using the allocated gRPC port.
  Check the `EndpointOptions.BasePort` range (default 4900-4999).
- Missing dependency service — the runner needs a service (e.g. measures needs the store service) that hasn't started yet.
  The coordinator starts runners in declaration order; make sure dependencies are declared first.

**Fix:** Read the runner's log output (it logs to stdout, same as the coordinator).
Enable `Debug` logging to see assembly resolution details.

---

## Runlet ordering errors

**Symptom:** `InvalidOperationException` at startup, or a runlet silently produces no output.

**Cause:** Some runlets must be declared in a specific order within their runner block.
For example, `signals` must come after `measures` in the same runner, and `event-persistence` must come after `events`.

**Fix:** See the [runlets catalog](../architecture/runlets.md) for ordering constraints on each runlet.

---

## Service discovery failures

**Symptom:** A runlet logs `Service 'xyz' not found` or a CLI command returns an empty result.

**Causes:**
- The runner hosting the target service hasn't started yet.
  Check `tw runners list` and `tw services list`.
- The service family name doesn't match — use the family name (e.g. `"store"`, `"measures"`) rather than the full protobuf service name.
- The coordinator pipe is unreachable — verify the coordinator is running and the pipe name matches.

---

## MQTT connection issues

**Symptom:** Log shows `MQTT connection failed` or `Retry N/M` messages.

**Causes:**
- Broker unreachable — check hostname, port, and network connectivity.
- Authentication failure — verify `username` and `password`.
  Credentials support `%ENV_VAR%` expansion; make sure the variable is set.
- Client ID conflict — two Tinkwell instances with the same `client-id` connecting to the same broker.

**Fix:** Test connectivity with `tw mqtt subscribe "#"` to verify the broker is reachable from the same machine.

---

## TLS / HTTPS errors

**Symptom:** gRPC calls between runners fail with TLS handshake errors.

**Causes:**
- Mismatched TLS modes — all runners must use the same `Tls.Mode` setting.
- Self-signed certificate not trusted — in `SelfSigned` mode, the OS trust store needs to contain the generated certificate.
  See [HTTPS / TLS](../reference/https.md) for OS-specific setup.
- Certificate expired — regenerate with `tw identity generate-cert`.

---

## Measures not updating

**Symptom:** `tw measures list` shows stale values or `tw measures watch` produces no output.

**Causes:**
- Integration binding misconfigured — the `bind measure` block's `name` expression doesn't match any defined measure.
- Measure not defined — the integration writes to a measure that doesn't exist in the config. Check `tw measures list` for defined names.
- Binding error swallowed — the default `on error resume next` policy logs a warning and continues.
  Check logs at `Warning` level.

---

## Plugin load failures

**Symptom:** `from "My.Plugin.dll"` fails with `FileNotFoundException` or the plugin's types aren't discovered.

**Causes:**
- Plugin directory name doesn't match `{name}@{version}` format.
- Missing `.deps.json` — recommended for plugins with complex dependency trees.
  Publish with `dotnet publish` to generate it.
- Version conflict — the plugin references a newer `Tinkwell.*` assembly than the host provides.
  Check log for version mismatch warnings.

**Fix:** Enable `Debug` logging and look for `Plugin catalog:` messages to see what was discovered.
See [Plugins](../reference/plugins.md#diagnostics) for diagnostic details.

---

## General debugging

- **Increase log level:** Set `Logging:LogLevel:Tinkwell` to `Debug` or `Trace` in `appsettings.json` next to the coordinator and runner executables (the same directory as `tw.exe` when you use a standard install, or your build output folder when running from source).
- **Check health:** `tw runners health` reports the status of all runners.
- **Pipe commands:** `tw raw "service list"` sends raw pipe commands for low-level diagnostics.
- **OpenTelemetry:** When telemetry is configured, metrics and traces are exported to your collector.
  See [Telemetry](../reference/telemetry.md).
