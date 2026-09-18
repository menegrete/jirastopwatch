## 1. Release pipeline: variant marker and checksums

- [x] 1.1 Add `-p:DefineConstants=STOPWATCH_SELF_CONTAINED` to the
      self-contained `publish()` call in `scripts/publish-release-artifacts.js`
- [x] 1.2 Expose `AppInfo.IsSelfContained` in `source/StopWatch/Helpers/AppInfo.cs`
      via `#if STOPWATCH_SELF_CONTAINED`
- [x] 1.3 In `scripts/publish-release-artifacts.js`, compute a SHA256 for each
      of the two built assets and write it as a `.sha256` file next to it
      (e.g. `JiraStopWatch-v{version}-self-contained.exe.sha256`)
- [x] 1.4 Add the two `.sha256` files to `.releaserc.json`'s
      `@semantic-release/github` `assets` list
- [x] 1.5 Verify locally: run the publish script, confirm both `.sha256` files
      are produced and match `Get-FileHash` on their asset

## 2. CheckForUpdates setting

- [x] 2.1 Add `CheckForUpdates` (bool, default `true`) to
      `source/StopWatch/Properties/Settings.settings`
- [x] 2.2 Read/write `CheckForUpdates` in `Settings.cs`'s `ReadSettings`/`Save`,
      following the existing boolean-setting pattern (e.g. `LoggingEnabled`)
- [x] 2.3 Expose a toggle for it in `SettingsWindow`

## 3. Update check

- [x] 3.1 Add a service that queries GitHub's `releases/latest` for this repo
      and returns the latest version tag and its asset list
- [x] 3.2 Add semantic-version comparison against `AppInfo.Version`
- [x] 3.3 Wire the check into app startup: async, non-blocking, only runs when
      `Settings.CheckForUpdates` is on, swallows and logs any failure
      (network error, rate limit, malformed response) without surfacing it
      to the user

## 4. Download and verification

- [x] 4.1 Given a newer release, pick the asset matching
      `AppInfo.IsSelfContained` (self-contained `.exe` vs
      framework-dependent `.zip`) by the existing
      `JiraStopWatch-v{version}-{variant}` naming convention
- [x] 4.2 Download that asset's `.sha256` file and the asset itself to
      `%LOCALAPPDATA%\StopWatch\updates\<version>\`
- [x] 4.3 Verify the downloaded asset's SHA256 against the downloaded
      checksum; on mismatch, discard both files and abort (no pending update)
- [x] 4.4 On any download/verification failure, clean up partial files and
      leave the app with no pending update (retry happens on next startup
      check)

## 5. Staging and apply

- [x] 5.1 For framework-dependent, extract the staged `.zip` into the staging
      directory so both variants end up with a ready-to-apply build on disk
- [x] 5.2 Generate a self-deleting `.cmd` helper script in `%TEMP%` that:
      waits (bounded, polling) for the current process ID to exit, applies
      the staged build (rename for self-contained; mirror add/replace/remove
      into the install directory for framework-dependent), relaunches
      `StopWatch.exe` from the install directory, then deletes itself
- [x] 5.3 On normal app exit with a verified update staged, launch the helper
      script detached (`Process.Start` with `UseShellExecute`, same pattern as
      `AppInfo.OpenUrl`) before the process ends
- [x] 5.4 Guard the apply path: if the install directory isn't writable,
      skip staging the helper launch entirely and leave the current
      installation untouched (no partial writes)

## 6. UI: update-ready notification

- [x] 6.1 Add a non-blocking banner/indicator (not a modal) shown once an
      update has been downloaded and verified, e.g. "vX.Y.Z lista — se aplica
      al reiniciar"
- [x] 6.2 Ensure the banner doesn't appear again for the same staged version
      after being shown once per session

## 7. Tests

- [x] 7.1 Unit test the semantic-version comparison logic (older/equal/newer,
      malformed tags)
- [x] 7.2 Unit test asset selection against `IsSelfContained` for both
      variants
- [x] 7.3 Unit test checksum verification (match, mismatch, missing checksum
      file)
- [x] 7.4 Unit test the startup check is skipped entirely when
      `CheckForUpdates` is off
- [ ] 7.5 Manually verify end-to-end on a real machine: run an older build,
      publish a newer test release, confirm check → download → banner →
      apply-on-restart → relaunch works for both the self-contained and
      framework-dependent variants
