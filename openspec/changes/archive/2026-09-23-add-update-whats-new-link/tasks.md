## 1. Thread the release URL through the update pipeline

- [x] 1.1 In `GitHubReleaseSource.GetLatestReleaseAsync`, read `html_url` from the GitHub API response alongside `tag_name` and `assets`.
- [x] 1.2 Add an `HtmlUrl` property to `ReleaseInfo` and populate it from 1.1.
- [x] 1.3 Add a `ReleaseUrl` property to `PendingUpdate`, threaded through its constructor.
- [x] 1.4 In `AutoUpdateService.DownloadAndStageAsync`, pass `release.HtmlUrl` into the `PendingUpdate` it constructs (both the self-contained and framework-dependent branches).

## 2. Show the "What's new" link

- [x] 2.1 In `MainWindow.xaml`, add a `Hyperlink` ("What's new") next to `lblUpdateReady`, following the existing `Hyperlink`/`RequestNavigate` pattern used in `SettingsWindow.xaml`. Keep it collapsed by default, alongside `lblUpdateReady`.
- [x] 2.2 In `MainWindow.xaml.cs`, add the `RequestNavigate` handler (opens `e.Uri` via `AppInfo.OpenUrl`, same as `SettingsWindow.Link_RequestNavigate`).
- [x] 2.3 In `CheckForUpdatesAsync`, set the hyperlink's `NavigateUri` to `update.ReleaseUrl` and make it visible together with `lblUpdateReady`.

## 3. Tests

- [x] 3.1 Update `AutoUpdateServiceTest`'s `ReleaseInfo` construction to include `HtmlUrl`, and assert the returned `PendingUpdate.ReleaseUrl` in the staging test(s) that reach a successful stage.
- [x] 3.2 Run `dotnet test StopWatch.sln --settings .runsettings` and confirm everything passes.

## 4. Manual verification

- [x] 4.1 Temporarily point `currentVersion` below the latest published GitHub release (or otherwise force a staged update) and confirm the "What's new" link appears next to the update-ready message and opens the correct GitHub release page in the default browser.
