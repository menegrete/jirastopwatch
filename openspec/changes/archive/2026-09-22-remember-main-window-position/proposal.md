## Why

The main window's width is remembered between runs, but its screen position and
maximized state are not — it always reopens wherever WPF's default placement
puts it, ignoring where the user last left it. `MiniTimerWindow` already solves
this exact problem for itself (`MiniViewLocation` + `Helpers/ScreenPlacement.cs`);
the main window never got the same treatment. Issue #29 asks for it.

## What Changes

- Persist the main window's position (`Left`/`Top`) as a single `"x,y"` string
  setting, `MainWindowLocation`, following the same shape as `MiniViewLocation`.
- Persist the main window's maximized/normal state as a new boolean setting,
  `MainWindowMaximized`.
- On startup, restore the remembered position and state, validating the
  position against currently connected screens via the existing
  `ScreenPlacement.EnsureOnScreen` (same multi-monitor safety net the mini view
  already relies on) so a window remembered on a since-disconnected monitor
  doesn't become unreachable.
- Save position and state on the same kind of triggers already used for width
  (`LocationChanged`/`StateChanged`, plus on close), mirroring the existing
  `MainWindow_SizeChanged` → `RestoreWidth()` pattern.
- Height stays derived from content (`SizeToContent="Height"`) and is
  unaffected — this only adds position and maximized state.

## Capabilities

### New Capabilities
- `main-window-placement`: the main window remembers its screen position and
  maximized/normal state between application runs, validated against the
  screens actually connected at startup.

### Modified Capabilities
(none — no existing capability spec currently governs main window
position/state)

## Impact

- `source/StopWatch/Properties/Settings.settings` / `Settings.Designer.cs`:
  two new user-scoped settings, `MainWindowLocation` (string) and
  `MainWindowMaximized` (bool).
- `source/StopWatch/Settings/Settings.cs`: new properties, read/written in
  `ReadSettings`/`Save`.
- `source/StopWatch/UI/MainWindow.xaml.cs`: save position/state on change and
  on close; restore and clamp them in `MainWindow_Loaded`, reusing
  `Helpers/ScreenPlacement.cs` (`TryParseLocation`, `FormatLocation`,
  `EnsureOnScreen`) rather than adding new geometry logic.
- No changes to `MiniTimerWindow`, `SettingsWindow`, `WorklogWindow`, or
  `EditTimeWindow` — those are out of scope (mini view already persists its
  own position; the three dialogs are fixed-size and always re-center on
  their owner by design).
