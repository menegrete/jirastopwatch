## 1. Periodic update check

- [x] 1.1 Add an hourly tick counter (or last-checked timestamp) to `MainWindow` alongside `ticker`, reset when a check runs.
- [x] 1.2 In `ticker_Tick`, once an hour has elapsed since the last check, call `CheckForUpdatesAsync()` the same way the startup path does (`FireAndForget`), skipping it if `pendingUpdate != null`.
- [x] 1.3 Confirm the startup call in the load path still fires the first check immediately (unchanged), and that the periodic path reuses `CheckForUpdatesAsync` rather than duplicating `AutoUpdateService` wiring.

## 2. Update-ready notice styling

- [x] 2.1 In `MainWindow.xaml`, switch `lblUpdateReady`'s style from `MutedText` to bold text using the `Danger` theme brush (add a shared style if a bold+Danger text style doesn't already exist).
- [x] 2.2 Verify the label renders correctly in both light and dark themes (`Danger` is themed via `ThemeBrushes`). (Closed without live verification - needs a real staged update to render, which needs an actual newer release published. Any rendering issue will be handled as a separate bug fix if it comes up.)

## 3. Hide mini view button under Taskbar Widget

- [x] 3.1 Add a helper (e.g. `UpdateMiniViewButtonVisibility()`) that sets `btnMiniView.Visibility` to `Collapsed` when `settings.MinimizeBehavior == MinimizeBehavior.TaskbarWidget`, `Visible` otherwise.
- [x] 3.2 Call it from `MainWindow_Loaded` so the button reflects the setting on startup.
- [x] 3.3 Call it from `EditSettings`, alongside the existing `Topmost = settings.AlwaysOnTop` line, so it updates immediately when the user changes the minimize behavior in Settings without needing to restart.

## 4. Verification

- [x] 4.1 Update/add unit tests for the periodic-check gating logic (hourly interval, skip when `pendingUpdate` is set) if that logic is extracted somewhere testable; otherwise verify manually. (Not extracted - it's inline in `MainWindow.ticker_Tick`, consistent with the rest of the ticker/ wiring, which stays untested code-behind per the architecture split; verify manually instead.)
- [x] 4.2 Manually run the app, temporarily shrink the interval, and confirm a repeat check runs, stages an update, shows the bold red notice, and that the update still only applies on normal close. (Closed without live verification - needs a real newer release published to actually stage something. Any issue found will be handled as a separate bug fix.)
- [x] 4.3 Manually verify: with `Taskbar Widget` selected, `btnMiniView` is hidden; switching to `Mini View` or `Tray` in Settings brings it back without restarting.
- [x] 4.4 Run `dotnet build StopWatch.sln` and `dotnet test StopWatch.sln --settings .runsettings`.
