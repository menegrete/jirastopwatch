## Why

Today the app only checks for a newer release once, at startup (`CheckForUpdatesAsync`, called from `MainWindow`'s load path right after `ticker.Start()`). Jira StopWatch is a tray app people commonly leave running for days, so a release published mid-session goes undetected until the user happens to restart — undercutting the point of auto-update. Rechecking periodically while the app is running lets long sessions pick up a new release without needing a restart to trigger the check.

Separately, with `Taskbar Widget` configured as the minimize behavior, the main window's explicit "mini view" toolbar button (`btnMiniView`) is still visible and still works, so clicking it opens the mini view on top of the taskbar widget — showing the same running timer twice at once. The button should not be shown at all while `Taskbar Widget` is the configured minimize behavior.

## What Changes

- Reuse the existing 30-second `ticker` in `MainWindow` (via a tick counter) to re-run the same staging check (`AutoUpdateService.CheckAndStageAsync`) every 1 hour, instead of adding a second `DispatcherTimer`.
- Skip the periodic check while a staged update is already pending (`pendingUpdate != null`) — no point re-checking once one is ready and waiting for the next normal close.
- No change to how or when a staged update is applied: it still only installs on a normal app close (`UpdateApplier.ApplyOnExit`), never interrupting a running session.
- Restyle `lblUpdateReady` (the "update ready to apply" notice) from its current muted/neutral text style to bold red (`Danger` theme brush), so a longer-running session — where the notice may now sit unnoticed for hours before the next close — is harder to miss.
- Hide the toolbar's "mini view" button (`btnMiniView`) whenever `settings.MinimizeBehavior == MinimizeBehavior.TaskbarWidget`, so it can no longer be used to duplicate the running timer into both the taskbar widget and the mini view at once. Re-show it for the other two minimize behaviors.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `auto-update`: the "La app chequea si hay una versión más nueva disponible" requirement currently only covers the check at startup; it needs a new scenario covering the periodic recheck while the app keeps running.
- `mini-timer-view`: the "El usuario entra y sale de la vista mini a voluntad" requirement currently says the main window unconditionally offers the mini-view control; it needs to state that this control is hidden while `Taskbar Widget` is the configured minimize behavior.

## Impact

- `source/StopWatch/UI/MainWindow.xaml.cs`: `CheckForUpdatesAsync`/its caller and the `ticker`'s tick handler; `btnMiniView`'s visibility, updated wherever `MinimizeBehavior` is read/applied (and whenever it changes via Settings).
- `source/StopWatch/UI/MainWindow.xaml`: `lblUpdateReady`'s style (currently `MutedText`) switches to bold + the `Danger` theme brush; `btnMiniView` gets a visibility binding/toggle.
- No changes to `AutoUpdateService`, `UpdateApplier`, or the settings schema — the periodic check is just a new caller of the same staging logic, gated by the same `settings.CheckForUpdates` flag. The mini-view button fix reads the existing `MinimizeBehavior` setting; it doesn't add a new one.
