## Why

When a staged update is ready to apply, the status bar tells the user a new
version is waiting, but gives no way to see what actually changed before
they restart. The release notes already exist (GitHub renders them on the
release page from the auto-generated changelog) — the app just doesn't link
to them.

## What Changes

- `GitHubReleaseSource` captures the release page URL (`html_url`) from the
  GitHub API response alongside the existing tag/assets data.
- `ReleaseInfo` and `PendingUpdate` carry that URL through the update-check
  pipeline.
- The "update ready" notice in `MainWindow`'s status bar gains a "What's
  new" hyperlink next to the existing text, opening the GitHub release page
  in the user's browser (reusing the existing `Hyperlink` /
  `AppInfo.OpenUrl` pattern already used in `SettingsWindow`).

## Capabilities

### Modified Capabilities
- `auto-update`: the "update ready" notification now includes a link to
  that release's notes, not just the version number.

## Impact

- `source/StopWatch/Update/GitHubReleaseSource.cs`: read `html_url` from the
  GitHub API response.
- `source/StopWatch/Update/ReleaseInfo.cs`: add a URL property.
- `source/StopWatch/Update/PendingUpdate.cs`: add a URL property, threaded
  through its constructor.
- `source/StopWatch/Update/AutoUpdateService.cs`: pass the URL through when
  building `PendingUpdate`.
- `source/StopWatch/UI/MainWindow.xaml` / `.xaml.cs`: render the hyperlink
  next to `lblUpdateReady`.
- Existing tests constructing `ReleaseInfo`/`PendingUpdate`
  (`AutoUpdateServiceTest`, `UpdateApplierTest`) need updating for the new
  property/constructor parameter.
