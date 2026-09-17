## Why

Releases today are entirely manual: nobody bumps `AssemblyInfo.cs`'s version, moves `CHANGELOG.md`'s `[Unreleased]` section to a version heading, runs `dotnet publish`, or uploads a build anywhere — CI only compiles and tests. Every push to `main` that changes the app should instead produce a versioned, changelog-documented GitHub Release with ready-to-run artifacts, with no manual steps.

## What Changes

- Add a release workflow, triggered on push to `main` restricted to paths under `source/StopWatch/**`, that runs `semantic-release`.
- `semantic-release` derives the next semver version from Conventional Commits (`feat`/`fix`/`BREAKING CHANGE`) since the last release tag; if no commit in range warrants a release, the workflow no-ops.
- **BREAKING**: `CHANGELOG.md` is no longer hand-maintained. Its `[Unreleased]` section and the convention of manually renaming it on release (documented in `CLAUDE.md`) are replaced by `semantic-release` generating and committing changelog entries directly from commit messages.
- The release commit updates `AssemblyVersion`/`AssemblyFileVersion`/`AssemblyInformationalVersion` in `source/StopWatch/Properties/AssemblyInfo.cs` and `CHANGELOG.md`, then tags and pushes back to `main`.
- The workflow builds and publishes two artifacts per release, attached to the GitHub Release:
  - a self-contained single-file executable (`dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true`)
  - a zip of the framework-dependent single-file publish output (`dotnet publish -r win-x64 --self-contained false -p:PublishSingleFile=true`)
- The release workflow only runs after `build.yml`'s test job passes on `main` — it does not release untested code.
- **Removed**: `.github/workflows/dispatch.yml` (generic cross-repo event broadcaster, unrelated to this app, superseded by nothing — it is simply deleted).
- **Removed**: the `dependency-scan` job in `.github/workflows/codeql.yml` (duplicates the native, hosted Dependabot `github-actions` update already configured in `.github/dependabot.yml`).
- `CLAUDE.md`'s changelog/versioning guidance is updated to describe the automated flow instead of the manual one.
- The main window's native title bar displays the app's version next to its title (e.g. "Jira StopWatch v2.4.0"), read at runtime from the same assembly version metadata this change automates.

## Capabilities

### New Capabilities
- `automated-releases`: pushing app changes to `main` automatically produces a versioned GitHub Release with a generated changelog entry and both build artifacts attached, gated on tests passing and on there being a releasable change.

### Modified Capabilities
(none — no existing spec describes CI/CD or release behavior)

## Impact

- **New**: `.github/workflows/release.yml` (or equivalent), `package.json`, `.releaserc.json` (or `release.config.js`) at repo root, a Node.js setup step in CI.
- **Changed**: `.github/workflows/codeql.yml` (drop `dependency-scan` job), `CLAUDE.md` (versioning/changelog section), `source/StopWatch/Properties/AssemblyInfo.cs` (now machine-written on release instead of hand-edited), `CHANGELOG.md` (now machine-appended).
- **Removed**: `.github/workflows/dispatch.yml`.
- **Changed**: `source/StopWatch/UI/MainWindow.xaml`(.cs) (title bar shows the app version) and `source/StopWatch/Helpers/AppInfo.cs` (or a new helper) to read it at runtime.
- **Repo settings**: Actions "Read and write permissions" must be enabled so the release commit/tag/release can be pushed; branch protection on `main` must allow the release bot to push directly (or a PAT with bypass rights is needed).
- **No impact** on `build.yml`'s existing build/test matrix, `dependabot.yml`, or the app's own runtime behavior.
