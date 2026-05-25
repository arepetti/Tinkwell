# Contributing

Contributions are welcome.
Please open an issue or pull request on GitHub.

By participating in this project you agree to abide by the [Code of Conduct](CODE_OF_CONDUCT.md).

## Where to ask / report

- **Bugs, features, and questions** — open an issue using one of the templates in [.github/ISSUE_TEMPLATE/](.github/ISSUE_TEMPLATE/).
- **Security vulnerabilities** — do not open a public issue.
  Follow [SECURITY.md](SECURITY.md).
- **Code of Conduct concerns** — see the reporting section of [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
- **Sibling projects** (firmwareless, plugin registry, state machines, VS Code extension) — open issues on the relevant repository linked from [README.md](README.md) under *Related repositories*.

## Getting started

```bash
dotnet restore Tinkwell.slnx
dotnet build Tinkwell.slnx
dotnet test Tinkwell.slnx
```

The repository targets the .NET version declared in `global.json` / `Directory.Build.props`.
A working `dotnet` CLI of the matching major version is the only hard prerequisite.

## Branch model

Trunk-based:

- `main` is always releasable.
  CI runs on every push and every pull request to `main` (see [.github/workflows/ci.yml](.github/workflows/ci.yml)).
- Work on short-lived topic branches off `main`.
  Suggested naming: `feature/<slug>` for new work, `fix/<slug>` for bug fixes, `docs/<slug>` for documentation-only changes.
- Open a pull request back into `main` when the branch is ready.
  There are no long-lived release or integration branches; releases are cut directly from `main` (see *Release process* below).
- Forks are first-class: fork the repository, push your branch to your fork, and open a PR from there.

## Pull request expectations

- **One topic per PR.** Split unrelated changes into separate PRs; it makes review and revert trivial.
- **Descriptive title.** A conventional-commit-style prefix (`fix:`, `feat:`, `docs:`, `refactor:`, `test:`, `chore:`) is encouraged but not required.
- **Tests.** Behaviour changes need tests.
  Bug fixes should include a regression test.
  Pure refactors keep existing tests green.
- **Docs in the same PR.** If a change affects public APIs, `.tw` grammar, CLI behaviour, or configuration keys, update the relevant files under [docs/](docs/) in the same pull request.
  Breaking changes are flagged in [CHANGELOG.md](CHANGELOG.md) under a **Breaking changes** heading; see the *Status* section of [README.md](README.md) for the `0.x` stability posture.
- **Green CI.** The PR build must pass before review.
  CI runs with `TreatWarningsAsErrors` enabled, so new warnings block the merge.
- **Review.** [.github/CODEOWNERS](.github/CODEOWNERS) routes reviews to `@arepetti`.
  Please address review comments by pushing follow-up commits on the same branch; they will be squashed on merge.
- **Squash-merge by default.** Keep the commit on `main` self-contained and easy to revert.

## Code style

Follow the conventions documented in [docs/contributing/conventions.md](docs/contributing/conventions.md) — project naming, type suffixes, null checks, `.tw` kebab-case settings, etc.

## Build and CI

See [docs/contributing/pipelines.md](docs/contributing/pipelines.md) for the CI/CD workflows, how the change-detection gate works, and how releases are created.

## Release process

Only the maintainer cuts releases.
The authoritative description of the release flow — triggering the `Create release` workflow, NuGet publishing, platform packaging, version bumping, and known caveats — lives in [docs/contributing/pipelines.md](docs/contributing/pipelines.md).

Tinkwell is in its `0.x` series; breaking changes between minor versions are allowed and are listed under **Breaking changes** in each release's notes in [CHANGELOG.md](CHANGELOG.md).

## Governance

Tinkwell is maintained by a single maintainer today.
See [GOVERNANCE.md](GOVERNANCE.md) for the decision process, dispute resolution, and the conditions under which the project would move to a multi-maintainer or foundation model.
