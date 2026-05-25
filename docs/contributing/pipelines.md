# CI/CD Pipelines

Seven GitHub Actions workflows handle continuous integration, releases, NuGet publishing, platform packaging, container publishing, version management, and CI tool distribution.

## Overview

```
PR / push to main
  └─ ci.yml ─── build + test + coverage

Manual trigger (Actions tab → "Create release" → Run workflow)
  └─ create-release.yml ── reads VersionPrefix, creates tag + GitHub Release
       ├─ publish.yml ──────── pack changed libraries → NuGet
       │     └─ (on success) version-bump.yml ── bump VersionPrefix, auto-merge PR
       ├─ release-packages.yml ── self-contained binaries → GitHub Release assets
       └─ release-docker.yml ─── multi-arch Docker image → GHCR

Manual trigger (Actions tab → "Publish CI Tool" → Run workflow)
  └─ publish-tool.yml ── pack & push tinkwell-ci-package → NuGet
```

## CI (`ci.yml`)

| | |
|---|---|
| **Trigger** | PR to `main`, push to `main` |
| **Skipped when** | Title or commit message contains `[NO CI]` |

- **PR builds** run unit tests only (`--filter "Category!=Integration"`).
- **Push builds** run all tests (unit + integration).
- All CI builds pass `-p:ContinuousIntegrationBuild=true`, which activates `TreatWarningsAsErrors` in `src/Directory.Build.props`.
- Coverage is collected via Coverlet, merged with ReportGenerator, and posted as a sticky comment on PRs.
  A `coverage-report` artifact is uploaded on every run.

## NuGet publish (`publish.yml`)

| | |
|---|---|
| **Trigger** | GitHub Release published |
| **Checkout** | `fetch-depth: 0` (full history, all tags) |

### Change-detection gate

Libraries under `src/app/libs/` fall into two groups, declared per-csproj via the `<TinkwellPackageGroup>` property:

| Group | Meaning |
|-------|---------|
| `SDK` | Part of the Tinkwell product surface. Always packed on every release, at the product version, to stay in lockstep with the runtime. |
| `Standalone` | Reusable libraries with no hard tie to the Tinkwell runtime (e.g. `Tinkwell.Coap`, `Tinkwell.Modbus`, `Tinkwell.Expressions`). Packed only when something that affects them has changed since the previous tag. |
| `ExcludeFromRelease` | Not packed by this workflow (e.g. `Tinkwell.Build.Ci`, which has its own `publish-tool.yml`). |

A missing marker defaults to `ExcludeFromRelease` so a new, unclassified lib is never silently pushed to NuGet.

Detection is delegated to [.github/scripts/detect-libs.sh](https://github.com/arepetti/Tinkwell/blob/main/.github/scripts/detect-libs.sh), which:

1. Finds the previous tag: `git tag --sort=-v:refname | grep -E '^v?[0-9]' | sed -n '2p'`.
2. Reads `<TinkwellPackageGroup>` from every `src/app/libs/*/*.csproj`.
3. Emits all `SDK` csproj paths into `sdk_projects` unconditionally.
4. For every `Standalone` lib, marks it dirty if:
   - its own directory has diffs against the previous tag, or
   - any of its transitive `<ProjectReference>` dependencies (SDK or Standalone) is dirty.
   SDK libs are treated as implicitly dirty, so Standalone libs that consume them get repacked with a `PackageReference` pointing at the new version.
5. Emits dirty Standalone csproj paths into `standalone_projects`.
6. On the very first release (no previous tag), sets `pack_all=true` and emits every SDK and Standalone csproj.
7. If the shared `src/app/libs/Directory.Build.props` has any non-`VersionPrefix` change (authors, license, URLs, tags, etc.), also sets `pack_all=true`.

The pack step iterates over the union of `sdk_projects` and `standalone_projects`.
The pack version comes from the release tag (`-p:PackageVersion=<version>`), not from the `VersionPrefix` in `Directory.Build.props`.

The push step uses `--skip-duplicate` as a safety net in case a package version already exists on NuGet (e.g. after a re-run).

In practice this means:

- Every SDK library is versioned together with the product on every release.
- `Tinkwell.Expressions` (Standalone but depending on SDK libs through `Tinkwell.Core` / `Tinkwell.Telemetry`) is republished on every release too, because its `PackageReference` values need to move forward with the SDK.
- `Tinkwell.Coap`, `Tinkwell.Coap.Server`, `Tinkwell.Encoding`, `Tinkwell.Lwm2m`, `Tinkwell.Lwm2m.Server`, `Tinkwell.Modbus` are republished only when their own code (or another Standalone lib they transitively reference) has changed.

## Platform packages (`release-packages.yml`)

| | |
|---|---|
| **Trigger** | GitHub Release published (runs in parallel with `publish.yml`) |

Builds self-contained .NET binaries for four RIDs on `ubuntu-latest` (cross-compilation):

| RID | Artifact |
|-----|----------|
| `win-x64` | ZIP |
| `win-arm64` | ZIP |
| `linux-x64` | tarball + `.deb` |
| `linux-arm64` | tarball + `.deb` |

All artifacts are uploaded to the GitHub Release via `gh release upload`.

For Windows ZIPs, the workflow prints the SHA256 hash in the GitHub Actions step summary.
These hashes are needed when submitting the winget manifest to `microsoft/winget-pkgs` (see `packaging/winget/AdrianoRepetti.Tinkwell.yaml`).

## Docker image (`release-docker.yml`)

| | |
|---|---|
| **Trigger** | GitHub Release published (runs in parallel with `publish.yml` and `release-packages.yml`); also `workflow_dispatch` for manual rebuilds |

Builds and pushes a multi-arch container image to the GitHub Container Registry at `ghcr.io/<owner>/tinkwell`.

| Detail | Value |
|--------|-------|
| Dockerfile | [`packaging/docker/Dockerfile`](https://github.com/arepetti/Tinkwell/blob/main/packaging/docker/Dockerfile) |
| Platforms | `linux/amd64`, `linux/arm64` |
| Base image (runtime stage) | `mcr.microsoft.com/dotnet/runtime-deps:10.0-bookworm-slim` |
| Tags | `<version>`, `<major>.<minor>`, `latest` (only on the `release` trigger) |
| Auth | Uses `GITHUB_TOKEN` with `packages: write` permission |
| Cache | GitHub Actions cache, scope `tinkwell-base` |

The image is a runtime only: no `ensemble.tw` and no plugins are baked in. Users supply both via bind mount or by deriving from the base image — see [Running under Docker](../getting-started/docker.md). The `manual workflow_dispatch` accepts a `version` override (useful for republishing a tag without recreating the release) and a `push` boolean (set to `false` to dry-run the build without uploading).

## Version bump (`version-bump.yml`)

| | |
|---|---|
| **Trigger** | After `publish.yml` completes successfully (`workflow_run`) |

1. Fetches the release tag from the triggering workflow run via the GitHub API.
2. Computes the next patch version (e.g. `0.1.0` -> `0.1.1`).
3. Updates `VersionPrefix` in both `src/app/Directory.Build.props` and `src/app/libs/Directory.Build.props`.
4. Opens a PR titled `chore: Update libs version to X.Y.Z [NO CI]` and enables auto-merge (squash).

The `[NO CI]` tag causes CI to skip the version-bump PR, so it merges immediately if branch protection allows skipped checks.

## Create release (`create-release.yml`)

| | |
|---|---|
| **Trigger** | Manual (`workflow_dispatch` — Actions tab → "Create release" → Run workflow) |

This is the recommended way to create a release.
It reads the version from the code so there is no risk of a mismatch between the tag and `VersionPrefix`:

1. Reads `VersionPrefix` from `src/app/libs/Directory.Build.props`.
2. Checks that the tag `v{version}` does not already exist (fails with an error if it does).
3. Creates a GitHub Release with auto-generated release notes, which triggers `publish.yml` and `release-packages.yml`.

## Creating a release

1. Go to **Actions > Create release > Run workflow**.
2. The workflow reads `VersionPrefix` (e.g. `0.1.0`), creates tag `v0.1.0` and a GitHub Release.
3. `publish.yml` detects which libraries under `src/app/libs/` have changed since the previous tag and packs only those.
   If none changed, NuGet is skipped.
4. `release-packages.yml` builds platform binaries (win-x64, win-arm64, linux-x64, linux-arm64) and uploads ZIPs, tarballs, and `.deb` packages.
5. `release-docker.yml` builds and pushes the multi-arch Docker image to GHCR.
6. On success, `version-bump.yml` bumps `VersionPrefix` to `0.1.1` and opens an auto-merging PR.

You can still create a release manually via **Releases > Draft a new release** if needed, but the `create-release.yml` workflow is preferred because it guarantees the tag matches the version in the code.

## CI tool publish (`publish-tool.yml`)

| | |
|---|---|
| **Trigger** | Manual (`workflow_dispatch` — Actions tab → "Publish CI Tool" → Run workflow) |

Packs and publishes the `Tinkwell.Build.Ci` global tool (the `tinkwell-ci-package` command) to NuGet.
The version is specified as an input parameter when triggering the workflow.

This is a manual workflow because the CI tool follows its own release cadence, independent of the main Tinkwell release.
It packs only `src/app/libs/Tinkwell.Build.Ci/Tinkwell.Build.Ci.csproj` and pushes the resulting `.nupkg` to NuGet with `--skip-duplicate`.

## Known caveats

### Version bump and change detection

The version-bump workflow edits `src/app/libs/Directory.Build.props` after every release.
The change-detection script diffs this file while **ignoring the `VersionPrefix` line**, so the auto-bump does not trigger a false-positive "all libraries changed" on every subsequent release.
Any other change in the shared props file (authors, license, URLs, tags, etc.) still forces `pack_all=true`.

### PackageVersion vs. VersionPrefix

`publish.yml` passes `-p:PackageVersion=<tag>` explicitly, so the NuGet package version always matches the release tag.
The `VersionPrefix` in `Directory.Build.props` is only used for local development builds (`dotnet build` / `dotnet pack` without an explicit version override).

### Shared metadata changes

If you change shared metadata in `src/app/libs/Directory.Build.props` (author, license, URLs, tags, etc.), the change-detection script detects the non- `VersionPrefix` diff and automatically packs every SDK and Standalone lib.
No manual workaround is needed.

### Future: per-library repositories

The gate above is correctness-safe: a change in any Tinkwell lib propagates to every dependent lib via the `<ProjectReference>` graph, so stale `PackageReference` versions on NuGet are not possible.
Splitting the Standalone libraries (`Tinkwell.Coap`, `Tinkwell.Lwm2m`, `Tinkwell.Modbus`, etc.) into their own repositories is therefore a product-shaping decision (release cadence, discoverability, issue triage) rather than a bug workaround.
It remains an option post-1.0 once the protocol APIs stabilise.

## One-time repository setup

These steps are needed once when creating the GitHub repository.

### Secrets

Go to **Settings > Secrets and variables > Actions** and add:

| Secret | Description |
|--------|-------------|
| `NUGET_API_KEY` | API key from [nuget.org](https://www.nuget.org/account/apikeys). Scope it to push packages for the `Tinkwell.*` prefix. |

### Workflow permissions

Go to **Settings > Actions > General > Workflow permissions** and select:

- **Read and write permissions**
- Check **Allow GitHub Actions to create and approve pull requests**

This is needed for the version-bump workflow to create branches, push commits, and open PRs using `GITHUB_TOKEN`.

### Branch protection (optional but recommended)

Go to **Settings > Branches > Add branch protection rule** for `main`:

- **Require a pull request before merging** -- enabled
- **Require status checks to pass before merging** -- enabled, add the `Test` check
- **Allow auto-merge** -- must be enabled at the repo level: **Settings > General > Pull Requests > Allow auto-merge**

The version-bump PRs have `[NO CI]` in their title, which causes the CI workflow to skip.
Since no required checks run, the auto-merge proceeds immediately.

If you configure `Test` as a required check, the `[NO CI]` skip will mark the job as skipped (not failed), and GitHub treats skipped required checks as passing -- so auto-merge still works.

### Code coverage

Coverage works out of the box with no external accounts:

- **How it works:** All test projects already include `coverlet.collector`.
  The CI workflow collects Cobertura XML during `dotnet test`, merges reports with ReportGenerator, and posts a coverage summary as a sticky comment on each PR.
- **Coverage artifacts:** Every CI run uploads a `coverage-report` artifact with the merged Cobertura XML -- useful for trend tracking or integration with other tools.
- **Optional: Codecov integration** -- If you later want historical trends and badges, sign up at [codecov.io](https://codecov.io), add a `CODECOV_TOKEN` secret, and add a step to upload.
  Not required for the basic setup.
