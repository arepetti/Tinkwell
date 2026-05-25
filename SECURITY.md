# Security Policy

## Supported versions

Tinkwell is in its `0.x` series (see [CHANGELOG.md](CHANGELOG.md) and the status section of [README.md](README.md)).
Security fixes target the latest minor release on `main`.
There is no back-porting commitment for older `0.x` minors at this stage; upgrade to the latest release to pick up fixes.

## Reporting a vulnerability

Please **do not** open a public GitHub issue for security problems.

Use GitHub's private vulnerability reporting on the repository:

- Open <https://github.com/arepetti/Tinkwell/security/advisories/new> (Repository → Security → Advisories → *Report a vulnerability*).

If GitHub private reporting is unavailable for any reason, contact the maintainer directly through the profile links on
<https://github.com/arepetti>.

Please include:

- A description of the issue and the potential impact.
- Steps to reproduce, ideally with a minimal `.tw` configuration or code sample.
- The affected component(s): runtime, CLI, `Tinkwell.Package`, a specific runlet, plugin loader, etc.
- The version or commit hash you tested against, and the OS / .NET runtime.

## What to expect

- Acknowledgement of the report within a reasonable time frame (target: a few working days).
- Coordinated disclosure: a fix and advisory will be published together, after which credit is attributed to the reporter unless they prefer otherwise.
- Tinkwell is a solo-maintained open-source project; response times are best-effort, not contractual.

## Scope

In scope for this policy:

- The Tinkwell runtime, CLI, and libraries in `src/` and `tests/`.
- The `Tinkwell.Package` signing / verification pipeline.
- The plugin loader and `AssemblyLoadContext` isolation boundary.

Out of scope (those projects have their own reporting channels, usually on their own repositories):

- `extras/firmwareless/*` — the firmware-less IoT platform.
- `extras/tinkwell-plugins/*` — the plugin registry reference implementation.
- `extras/statemachines/*` — the state-machines compiler.
- Sibling repositories linked from [README.md](README.md) (e.g. `tinkwell-firmwareless-*`, `tinkwell-plugins-repository`, `tinkwell-static-plugins-registry`).

Report those on the repository that owns the code.
