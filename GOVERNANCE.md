# Governance

This document describes how Tinkwell is maintained today and how decisions are taken.
It is deliberately short and honest: the project has one maintainer, and the governance process reflects that.

See also [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md), and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## Scope

This document covers **Tinkwell itself** — the runtime, CLI, and libraries shipped from `Tinkwell.slnx`.
The `extras/` solutions and the sibling repositories linked from [README.md](README.md) each have their own governance; issues for them should go to those repositories.

## Maintainer

Tinkwell is currently maintained by a single person:

- **Adriano Repetti** (`@arepetti`) — project creator and maintainer.

The maintainer has the final say on scope, design, merges, and releases, and holds the release signing keys.

## Roles

There are two roles:

- **Maintainer.** Merges pull requests, cuts releases, owns the scope of the project.
  Identified in [.github/CODEOWNERS](.github/CODEOWNERS).
- **Contributor.** Anyone who opens an issue or pull request.
  Contributors do not have merge rights; review is performed by the maintainer.

No separate "committer" or "triager" role exists yet.
If the project grows beyond what one maintainer can handle, this document will be updated before the role is handed out.

## Decision process

Day-to-day decisions are recorded in GitHub issues and pull requests.
The rules of thumb are:

- **Small changes** (bug fixes, doc fixes, tests, internal refactors): open a PR.
  Discussion happens on the PR.
- **User-visible changes** (public APIs, `.tw` grammar, the `Tinkwell.Package` format, CLI behaviour, configuration keys): open an issue first so the design is discussed in writing before code is written.
  The rationale for the accepted design goes in the PR description.
  Docs are updated in the same PR.
- **Breaking changes** while the project is in its `0.x` series are listed in [CHANGELOG.md](CHANGELOG.md) under a **Breaking changes** heading in the release notes for the version that introduces them.
  See the *Status* section of [README.md](README.md) for the stability posture.

The maintainer reserves the right to decline changes that do not fit the project's scope or direction.
When that happens, the reason goes in the PR or issue thread, not in a private channel.

### Response times

This is a solo-maintained open-source project; responses are best-effort.
Security reports (via [SECURITY.md](SECURITY.md)) are prioritised over feature work, but there is no contractual SLA.

## Release authority

Only the maintainer cuts releases.
The release procedure, from triggering the `Create release` workflow to version bumping, is documented in [docs/contributing/pipelines.md](docs/contributing/pipelines.md) — that is the authoritative source of truth for the mechanics.

The maintainer holds the release signing keys (NuGet API key today; GPG, Authenticode, and winget-related credentials as they come online).

## Dispute resolution

Project-level disputes are resolved on the GitHub thread where they arise.
In a disagreement between contributors and the maintainer, the maintainer has final say on project direction, but the conversation stays public and on-record.
Behaviour complaints are handled under [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md); the right of anyone to fork the project under the [MIT](LICENSE) license is unconditional.

## Bus factor

The bus factor of Tinkwell is **1**.
This is a known risk for anyone considering Tinkwell for industrial, production, or otherwise dependency-critical use cases.
Mitigations today:

- The project is MIT-licensed, so a fork is always possible.
- Every release is published to GitHub Releases and nuget.org; artefacts and source history are durable independent of the maintainer.
- Documentation is treated as a first-class deliverable (see [docs/](docs/)) so a successor maintainer or a fork has a reasonable starting point.

The project is not suitable for workloads that cannot tolerate "one maintainer goes on holiday" response times.

## What would change under a foundation or multi-maintainer model

If Tinkwell outgrows solo maintenance — typical triggers: industrial adopters who need a governance story, a second long-term maintainer, or a consortium interested in underwriting the project — this document will be replaced with a formal governance model.
The likely changes are:

- A named **maintainer team** with a minimum size (two or three), voting rules for non-trivial decisions, and a documented quorum.
- **Trademark**, name, and any project domains transferred to a neutral legal entity (a foundation, a non-profit, or a holding company created for the purpose).
- **Release signing keys** — GPG for `.deb` and tarball artefacts, Authenticode for Windows binaries, NuGet author-signing when it lands — rotated from personal keys into foundation-held hardware tokens, with a documented rotation cadence.
- A **published meeting cadence** (even if it starts as "monthly async office hours"), a **public roadmap** living in the repository, and oversight of the conformance test kit if the ecosystem grows one.
- **Right-to-fork** is already guaranteed by the MIT license today and does not depend on any of the above happening.

None of this is on the roadmap right now.
It will be triggered by circumstances, not by a target date; when it starts, the process will be transparent and happen in public on the repository.

## Related repositories

Tinkwell is part of a family of related projects (listed under *Related repositories* in [README.md](README.md)).
Each of those repositories has its own maintainers, issue trackers, and governance.
Cross-cutting decisions affecting multiple repositories are coordinated by the maintainer(s) on whichever repository is primary for the change.
