## Context

Jira StopWatch ships two GitHub Release assets per version, built by
`scripts/publish-release-artifacts.js` and named
`JiraStopWatch-v{version}-self-contained.exe` and
`JiraStopWatch-v{version}-framework-dependent.zip` (see
`automated-releases`). There is no installer: users run whichever asset they
downloaded from wherever they extracted it, typically a user-writable
directory (no evidence of `Program Files`/admin installs in this project).
Per-user settings already live outside the app directory
(`ConfigurationUserLevel.PerUserRoamingAndLocal`, `Settings.cs`), so replacing
every file in the install directory does not touch user data.

`AppInfo.Version` (`Helpers/AppInfo.cs`) already reads the running version
from `AssemblyInformationalVersionAttribute`. Nothing today lets the running
process tell whether it *is* the self-contained or framework-dependent build
— both publish as a single visible `StopWatch.exe`, distinguishable in
practice only by file size.

## Goals / Non-Goals

**Goals:**
- Detect a newer GitHub Release at startup without blocking UI or failing
  loudly.
- Fetch and verify the correct asset for the variant currently running.
- Apply the update without ever touching a build the process is still using,
  and without silently killing a running timer.
- Let users opt out via a setting.

**Non-Goals:**
- Elevation / support for installs in admin-only directories. If the install
  directory isn't writable, auto-update degrades to notify-and-link; making
  that case work (UAC prompt, installer, etc.) is out of scope.
- Delta/binary-diff updates. Each update downloads the full asset.
- Update channels (beta/pre-release). Only GitHub's `releases/latest`
  (non-prerelease) is considered.
- Code-signing the executable. Integrity is checksum-based (see Decisions),
  not signature-based.

## Decisions

### Variant detection: MSBuild constant, not a runtime heuristic

`scripts/publish-release-artifacts.js`'s self-contained `publish()` call gains
`-p:DefineConstants=STOPWATCH_SELF_CONTAINED`. `AppInfo.cs` exposes:

```csharp
public static bool IsSelfContained =
#if STOPWATCH_SELF_CONTAINED
    true;
#else
    false;
#endif
```

Alternative considered: inferring the variant from the running executable's
file size (self-contained is ~150MB, framework-dependent a few MB). Rejected
— it's a magic-number heuristic that silently breaks if the size profile of
either build changes, versus a one-line, self-documenting build flag using an
MSBuild mechanism (`DefineConstants`) already used elsewhere in the csproj
(the `WPF0001` `NoWarn` case).

### Checksums: SHA256 published alongside each asset, not code signing

`publish-release-artifacts.js` writes a `.sha256` file next to each of the
two assets after they're built; `.releaserc.json`'s `@semantic-release/github`
`assets` list attaches those four files (two builds, two checksums) to the
release. The app downloads the checksum file for its target asset first (tiny,
cheap) and verifies the downloaded asset against it before staging.

Alternative considered: Authenticode-signing `StopWatch.exe`. Rejected for
this change — it requires acquiring and managing a code-signing certificate,
which is a project/cost decision beyond an update mechanism, and unsigned
releases are what's shipped today. Checksums stop a corrupted-download apply,
which is the realistic failure mode; they don't stop a compromised GitHub
account from shipping a bad release — that risk exists identically today for
anyone downloading the asset by hand.

### Download staging: outside the install directory, applied by a detached helper

The new asset downloads to `%LOCALAPPDATA%\StopWatch\updates\<version>\`,
never into the install directory while the app is running. Once verified and
staged, applying means the *next* normal exit runs a small detached helper
(a generated, self-deleting `.cmd` in `%TEMP%`, launched the same way
`AppInfo.OpenUrl` already shells out via `Process.Start`/`UseShellExecute`)
that:
1. waits for the StopWatch process ID to exit (poll, short timeout),
2. self-contained: renames the staged `.exe` over the installed one (same
   volume, effectively atomic);
   framework-dependent: extracts the staged `.zip` and mirrors it into the
   install directory (add/replace/remove to match the new build exactly),
3. relaunches `StopWatch.exe` from the install directory,
4. deletes itself.

Alternative considered: a dedicated small updater *executable* (its own
project) instead of a generated script. Rejected for v1 — more to build,
sign, and ship as a third release asset; a script covers both variants'
swap logic (single rename vs. directory mirror) without new build/release
plumbing. Worth revisiting if script quoting/escaping around paths with
spaces proves fragile in practice.

### Trigger and UX: check + background download on startup, apply only on restart

The check and download happen automatically (respecting the new
`CheckForUpdates` setting); *applying* never happens while the app is running
or by force-closing it — only when the user next exits normally. This avoids
ever interrupting a running timer to install an update. Once staged, a
non-blocking banner (not a modal) shows "vX.Y.Z lista — se aplica al
reiniciar", consistent with the app's existing non-intrusive notification
style.

### CheckForUpdates setting

Follows the existing `Settings.cs` pattern exactly: a new boolean entry in
`Properties/Settings.settings` (default `true`), read/written in
`Settings.cs`'s `ReadSettings`/`Save` alongside the other simple boolean
settings (e.g. `LoggingEnabled`), exposed in `SettingsWindow`.

## Risks / Trade-offs

- **Unwritable install directory** (e.g. extracted under `Program Files`) →
  the apply step's rename/mirror fails; caught and logged, app falls back to
  today's fully-manual flow (no retry loop, no elevation prompt).
- **Partial/corrupted download** → checksum mismatch aborts staging before
  anything touches the install directory; no partial apply is possible.
- **Update available but user force-kills the app / it crashes** → the
  detached helper's process-exit wait uses a bounded timeout and gives up
  (leaving the old build in place) rather than hanging indefinitely or
  killing the process itself.
- **GitHub API rate limiting** (60 unauthenticated requests/hour/IP) → a
  once-per-launch check is well within budget for a single-user desktop app;
  a failed check just skips silently and retries on the next launch.
- **Generated helper script as an attack surface** → it's written to `%TEMP%`
  with content the app fully controls (no user/network input interpolated
  into it beyond the already-checksum-verified staged path), then deleted
  after one use.
- **Framework-dependent directory mirror deletes files unexpectedly** →
  scoped strictly to the install directory the running `StopWatch.exe` lives
  in, mirroring only against the known contents of the verified staged zip;
  no recursive delete outside that directory.

## Migration Plan

No data migration. Existing installs simply start checking for updates on
their next launch (or never, if `CheckForUpdates` is turned off). Rollback is
deleting the new update-check code path and the `CheckForUpdates` setting;
nothing persisted by this feature lives outside
`%LOCALAPPDATA%\StopWatch\updates\`, which is safe to leave orphaned or clean
up in a follow-up.

## Open Questions

- Exact polling/backoff behavior if the detached helper's process-exit wait
  times out repeatedly across several launches — acceptable to leave as
  "give up once per attempt, retry next launch" unless testing shows that's
  disruptive.
