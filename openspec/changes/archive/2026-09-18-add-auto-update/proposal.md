## Why

Jira StopWatch has no installer and no update mechanism: users get a version by
manually downloading a GitHub Release asset, and every subsequent release
requires them to notice it exists and repeat that manually. Now that releases
are fully automated and versioned (see `automated-releases`), the app itself
can close that loop — check for a newer release, fetch it, and apply it on its
own, without the user needing to know a new version shipped.

## What Changes

- The app checks GitHub Releases for a newer version at startup (non-blocking,
  fails silently on network/rate-limit errors) and compares it against the
  running `AppInfo.Version`.
- When a newer release exists, the app downloads the release asset matching
  the variant it's currently running (self-contained `.exe` vs
  framework-dependent `.zip`), verifies it against a published SHA256
  checksum, and stages it without disturbing the running instance.
- The update is applied via a detached helper process that waits for the app
  to exit, swaps the staged build into the install directory (a single-file
  rename for self-contained; a directory sync for framework-dependent), and
  relaunches the app. Nothing is applied while a timer could be running —
  only on the next normal exit/restart.
- A new **CheckForUpdates** setting (default on) lets users opt out entirely,
  following the existing `Settings.cs` read/write pattern.
- The release pipeline gains a build-time marker
  (`-p:DefineConstants=STOPWATCH_SELF_CONTAINED` on the self-contained publish
  in `scripts/publish-release-artifacts.js`) so the running app can tell which
  variant it is, and publishes a SHA256 checksum for each release asset so the
  app can verify what it downloaded before applying it.
- If anything about the check, download, verification, or apply step fails
  (unwritable install directory, checksum mismatch, network error), the app
  falls back to today's fully-manual path: no crash, no partial swap.

## Capabilities

### New Capabilities
- `auto-update`: checking for newer releases, downloading and verifying the
  matching build variant, and applying it via a detached helper on restart.

### Modified Capabilities
- `automated-releases`: each release must also publish a SHA256 checksum for
  its two build assets, and the self-contained build must be distinguishable
  from the framework-dependent one at runtime via a build-time marker.

## Impact

- `source/StopWatch/Helpers/AppInfo.cs`: exposes `AppInfo.IsSelfContained`.
- `source/StopWatch/StopWatch.csproj`: conditional compilation symbol wiring.
- `scripts/publish-release-artifacts.js`: self-contained publish gains
  `-p:DefineConstants=STOPWATCH_SELF_CONTAINED`; both publishes gain a SHA256
  checksum step for their asset.
- `.releaserc.json`: `@semantic-release/github` `assets` list gains the
  checksum files.
- `source/StopWatch/Settings/Settings.cs`, `Properties/Settings.settings`:
  new `CheckForUpdates` setting.
- New model/service code for the update check, download, verification, and
  the detached apply helper (exact placement to be decided in design.md).
- `App.xaml.cs` / `MainWindow.xaml.cs`: wiring the startup check and the
  non-intrusive "update ready, restart to apply" notification.
