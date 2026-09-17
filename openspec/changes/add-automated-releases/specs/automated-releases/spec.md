## Purpose

Automates producing a versioned, changelogged GitHub Release with runnable build artifacts whenever a releasable change to the app lands on `main`, replacing the fully manual version-bump/changelog/publish process.

## ADDED Requirements

### Requirement: Release trigger is scoped to app source changes
The release process SHALL only be evaluated for pushes to `main` that include at least one changed file under `source/StopWatch/**`.

#### Scenario: Push touches app source
- **WHEN** a push to `main` includes a commit that modifies a file under `source/StopWatch/`
- **THEN** the release process is evaluated for that push

#### Scenario: Push touches unrelated files only
- **WHEN** a push to `main` only modifies files outside `source/StopWatch/` (for example, `openspec/`, `CLAUDE.md`, or `.github/`)
- **THEN** the release process is not evaluated and no release is produced

### Requirement: Release is only produced from a releasable commit history
The release process SHALL determine the next version from Conventional Commit messages (`feat`, `fix`, `BREAKING CHANGE`, etc.) made since the last release tag, and SHALL NOT produce a new version, tag, or release when no commit in that range warrants one.

#### Scenario: Releasable commits present
- **WHEN** the commit history since the last release tag contains at least one `feat` or `fix` commit (or a breaking-change commit)
- **THEN** a new semantic version is computed (major for breaking changes, minor for `feat`, patch for `fix`) and a release is produced

#### Scenario: No releasable commits
- **WHEN** the release process is evaluated but the commit history since the last release tag contains only non-releasable commits (for example, `chore`, `docs`, `refactor`, `test`)
- **THEN** no version bump, tag, or release is produced

### Requirement: Release is gated on tests passing
The release process SHALL NOT produce a release for a commit whose automated test run has not passed.

#### Scenario: Tests pass
- **WHEN** the test suite for the pushed commit on `main` passes
- **THEN** the release process may proceed to evaluate whether a release is warranted

#### Scenario: Tests fail
- **WHEN** the test suite for the pushed commit on `main` fails
- **THEN** no release is produced for that commit, regardless of commit content

### Requirement: Released version is recorded in the app and changelog
When a release is produced, the system SHALL update the app's version metadata and changelog to reflect the new version, and SHALL persist those updates back to `main`.

#### Scenario: Version metadata updated
- **WHEN** a new version `X.Y.Z` is released
- **THEN** `AssemblyVersion`, `AssemblyFileVersion`, and `AssemblyInformationalVersion` in the app's assembly metadata are set to `X.Y.Z`

#### Scenario: Changelog entry generated
- **WHEN** a new version `X.Y.Z` is released
- **THEN** a changelog entry for `X.Y.Z` is generated from the Conventional Commit messages included in that release and added to the project's changelog, without requiring manual editing

### Requirement: Release includes two runnable build artifacts
Each produced release SHALL attach a self-contained executable and a zipped framework-dependent build to the GitHub Release.

#### Scenario: Self-contained artifact attached
- **WHEN** a release is produced
- **THEN** a self-contained single-file Windows executable (no separate .NET runtime install required) is attached to the release

#### Scenario: Framework-dependent artifact attached
- **WHEN** a release is produced
- **THEN** a zip archive of the framework-dependent single-file build (requires the matching .NET Desktop Runtime on the target machine) is attached to the release

### Requirement: Running app displays its own version
The main window SHALL show the app's current version in its native title bar, next to the app title, read from the running assembly's version metadata.

#### Scenario: Title bar shows version
- **WHEN** the main window is displayed
- **THEN** its title bar text includes both the app name and the running assembly's version (for example, "Jira StopWatch v2.4.0")
