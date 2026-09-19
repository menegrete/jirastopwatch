## 1. Settings

- [x] 1.1 Add `TaskbarWidget` to the `MinimizeBehavior` enum in `Settings/Settings.cs`
- [x] 1.2 Add `TaskbarWidgetMonitors` to `Properties/Settings.settings` (comma-separated string of monitor indices) and regenerate `Settings.Designer.cs`
- [x] 1.3 Add a `TaskbarWidgetMonitors` (`List<int>`) property to `Settings.cs` that parses/formats the raw string, defaulting to the primary monitor (`[0]`) when empty or unparseable

## 2. Pure placement math (`Helpers/TaskbarPlacement.cs`)

- [x] 2.1 Function: given the taskbar rect, anchor points (widgets-button-right, start-left, task-buttons-right), DPI scale and window size, compute the target top-left position and whether it fits (left-aligned vs centered taskbar)
- [x] 2.2 Function: given the taskbar's monitor bottom, work-area bottom and rect, compute the visible-pixels band and the reserved taskbar height (mirrors the reference's `GetTaskbarVisiblePx`)
- [x] 2.3 Function: given visible-pixels vs full taskbar height, compute the auto-hide animation phase (0..1) and the eased position for a given progress
- [x] 2.4 Unit tests for 2.1–2.3 covering: left-aligned taskbar, centered taskbar, narrow available space (below minimum), taskbar fully hidden, taskbar mid-slide — no real screen or taskbar required, same style as `ScreenPlacementTest`

## 3. Win32 interop (`Helpers/TaskbarInterop.cs`)

- [x] 3.1 `FindWindow`/`FindWindowEx` helpers to resolve `Shell_TrayWnd`, enumerate `Shell_SecondaryTrayWnd` (sorted by monitor position, not z-order), and find `TrayNotifyWnd`
- [x] 3.2 `SHAppBarMessage(ABM_GETSTATE)` wrapper for `IsAutoHideEnabled()`
- [x] 3.3 `GetWindowRect`/`GetMonitorInfo`/`GetDpiForWindow` wrappers
- [x] 3.4 `SetWindowLongPtr(GWLP_HWNDPARENT, ...)` ownership helper + `WS_EX_TOOLWINDOW`/`WS_EX_NOACTIVATE` style helper
- [x] 3.5 `SetWinEventHook`/`UnhookWinEvent` wrapper for `EVENT_OBJECT_LOCATIONCHANGE` (scoped to the target tray's thread/process) and for `EVENT_SYSTEM_FOREGROUND`
- [x] 3.6 `SetWindowRgn`-based clip helper for the slide animation, tracking clipped state per window handle
- [x] 3.7 Foreground-fullscreen check (`GetForegroundWindow` + monitor/rect comparison, excluding shell window classes), mirroring the reference's `IsForegroundFullscreen`

## 4. Anchor discovery (`Helpers/TaskbarAnchors.cs`)

- [x] 4.1 UI Automation lookup for `AutomationId` `WidgetsButton` and `StartButton`, plus the rightmost `Taskbar.TaskListButton`, given a tray window handle
- [x] 4.2 Return a tri-state result distinguishing "query failed" (keep previous anchors) from "element legitimately absent" (widgets disabled)
- [x] 4.3 Wrap per-button reads in their own try/catch so one dying button (app opening/closing) doesn't discard an otherwise-successful read

## 5. TaskbarWidgetWindow

- [x] 5.1 Create `UI/TaskbarWidgetWindow.xaml(.cs)`: borderless, topmost, single-row layout reusing the existing mini-timer row visuals (key, summary, elapsed time, running/paused color)
- [x] 5.2 Wire `ActiveTimerViewModel` the same way `MiniTimerWindow` does, with its own 1-second `DispatcherTimer` while visible
- [x] 5.3 Pause/resume control on the row, calling the same model methods `MiniTimerWindow`'s row toggle uses
- [x] 5.4 Double-click restores the main window (reuse `RestoreRequested` event pattern from `MiniTimerWindow`)
- [x] 5.5 Context menu: "Restore" and "Exit" entries, plus a "Monitor" submenu matching Settings' monitor picker (found missing during 9.3 manual verification, added after)
- [x] 5.6 On `OnSourceInitialized`, apply `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE` and set `GWLP_HWNDPARENT` to the resolved tray handle
- [x] 5.7 Position/visibility loop: resolve target tray → compute visible-pixels band (2.2) → if fully hidden and not auto-hide-animating, hide; if auto-hide is sliding, ride it (2.3 + interop 3.6); otherwise query anchors (throttled, 4.x) and position via 2.1
- [x] 5.8 Hide when anchors report "no room" (per spec) and when `TaskbarInterop` foreground-fullscreen check is true and auto-hide is off
- [x] 5.9 Recreate-on-close: if the window closes because its owning tray died (Explorer restart) rather than user/app-initiated close, signal `MainWindow` to recreate it

## 6. Multi-monitor wiring

- [x] 6.1 ~~One `TaskbarWidgetWindow` instance per monitor index~~ — superseded: monitor choice is single-select (see below), so there is only ever one instance
- [x] 6.2 Fallback: if the configured monitor is not connected, anchor to the primary tray instead (re-resolved fresh on every poll via `MainWindow.ResolveTargetTray`, so a disconnect recovers on its own without any resync event)
- [x] 6.3 ~~Re-sync instances on display-change~~ — superseded: no longer needed now that `ResolveTargetTray` re-resolves from scratch on every poll (removed the `SystemEvents.DisplaySettingsChanged` hook and the `TrayUnresolved` event this replaced)

## 7. Settings UI

- [x] 7.1 Add `Taskbar Widget` as a third choice to `cbMinimizeBehavior` in `SettingsWindow.xaml.cs`
- [x] 7.2 Add a "Monitor" control listing connected monitors as a single-select choice (radio buttons), bound to `Settings.TaskbarWidgetMonitor`, visible only when `Taskbar Widget` is selected (changed from a multi-select checkbox list after manual testing - the widget only ever shows on one monitor)
- [x] 7.3 Disable `cbAllowMultipleTimers` (and its dependent `gridMaxConcurrentTimers`) while `Taskbar Widget` is selected in `cbMinimizeBehavior`; re-enable when switched away
- [x] 7.4 Confirm `gridMinimizeBehavior`'s existing `IsWindowsEnvironment()` gate covers the new option (whole control, not just the new item)

## 8. MainWindow wiring

- [x] 8.1 Extend the minimize-with-native-control switch (`settings.MinimizeBehavior`) to handle `TaskbarWidget`, mirroring `EnterMiniView`/`ExitMiniView`
- [x] 8.2 `EnterTaskbarWidget()`/`ExitTaskbarWidget()`: hide/show `MainWindow`, create/dispose the `TaskbarWidgetWindow` instance(s) per section 6
- [x] 8.3 Restoring from the widget (double-click, context menu, or its own restore action) always returns to `MainWindow`, never to `MiniTimerWindow`

## 9. Verification

- [x] 9.1 `dotnet build StopWatch.sln` with `TreatWarningsAsErrors` clean
- [x] 9.2 `dotnet test StopWatch.sln --settings .runsettings` green, including the new `TaskbarPlacement` unit tests
- [x] 9.3 Manual: verify against a real taskbar — left-aligned and centered icon layouts, auto-hide on/off, multiple monitors with a monitor disconnected while selected, an app going fullscreen on the widget's monitor, `explorer.exe` restart while the widget is up (confirmed working; fixed the context menu's illegible text and a stuck-hidden bug on monitor disconnect along the way - see Styles.xaml and TaskbarWidgetWindow.xaml.cs)
- [x] 9.4 Manual: verify `AllowMultipleTimers` ⇄ `Taskbar Widget` mutual exclusion in both directions in Settings (fixed: selecting Taskbar Widget now clears the checkbox instead of only disabling it)
