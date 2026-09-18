## 1. Settings model

- [x] 1.1 Add a `MinimizeBehavior` `Int32` entry (default `0`) and a
      `MinimizeBehaviorMigrated` `Boolean` entry (default `False`) to
      `Properties/Settings.settings`; leave the existing `MinimizeToTray`
      entry in place, unused going forward except as a migration source.
- [x] 1.2 Add `enum MinimizeBehavior { MiniView, Tray }` and a strongly-typed
      `Settings.MinimizeBehavior` property in `Settings.cs` that casts
      to/from the persisted `Int32`, same pattern as `Theme`/`ListDensity`.
- [x] 1.3 In `Settings.cs`'s `ReadSettings`, when `MinimizeBehaviorMigrated`
      is `False`, seed `MinimizeBehavior` from `MinimizeToTray` (`true` →
      `Tray`, `false` → `MiniView`), set `MinimizeBehaviorMigrated = true`,
      and persist both via `Save()`.

## 2. Settings window

- [x] 2.1 Replace the `cbMinimizeToTray` checkbox in `SettingsWindow.xaml`
      (line 67) with a two-option combo (`Mini View` / `Tray`) bound to the
      new setting.
- [x] 2.2 Update `SettingsWindow.xaml.cs` (lines 61, 70, 117) to read/write
      the combo's selection instead of the checkbox's `IsChecked`, keeping
      the existing `IsWindowsEnvironment()` visibility gate.

## 3. Minimize routing

- [x] 3.1 In `MainWindow_StateChanged` (lines 320-343), replace
      `if (!settings.MinimizeToTray) return;` with a branch on
      `settings.MinimizeBehavior`: `Tray` keeps the existing
      `ShowTrayIcon(); Hide();` path; `MiniView` calls `EnterMiniView()`
      instead. Leave the `if (inMiniView) return;` guard untouched.
- [x] 3.2 Confirmed `EnterMiniView()`/`ExitMiniView()` need no changes for
      the `WindowState == Minimized` case reached via this new path (per
      design.md Decision 4) — code inspection only, no test added: no
      `Window` subclass in this codebase (`MainWindow`, `SettingsWindow`,
      `MiniTimerWindow`) has automated coverage today (see CLAUDE.md
      Architecture: only the Model/ViewModel layer is unit-tested), so
      adding one here for this class alone would be a new, unprecedented
      pattern rather than following an existing one.

## 4. Tests

- [x] 4.1 Unit test the `MinimizeToTray` → `MinimizeBehavior` migration:
      extracted as `Settings.MigrateMinimizeBehavior(bool)`, a static method
      testable without touching `Properties.Settings.Default`, covering both
      directions. The one-time gate around it in `ReadSettings` (skip if
      `MinimizeBehaviorMigrated`) is exercised only by manual verification
      (5.3/5.4) below, consistent with the rest of `ReadSettings`/`Save`
      never being unit-tested directly in this codebase.
- [ ] 4.2 ~~Test that `MainWindow_StateChanged` calls `EnterMiniView()`
      when `MinimizeBehavior == MiniView`, and takes the tray path when
      `== Tray`.~~ Not automatable without new WPF test infrastructure this
      codebase doesn't have (see 3.2) — covered by manual verification
      5.2/5.3 instead.
- [ ] 4.3 ~~Regression test that minimizing while already `inMiniView`
      stays a no-op.~~ Same as 4.2 — the `if (inMiniView) return;` guard is
      untouched code, unexercised by any existing automated test either.

## 5. Verification

- [x] 5.1 `dotnet build StopWatch.sln` and
      `dotnet test StopWatch.sln --settings .runsettings` pass. (0 warnings,
      0 errors; 157 passed, 5 skipped — pre-existing skips, unrelated.)
- [x] 5.2 Manually verify with a fresh settings file: the combo defaults to
      `Mini View`, and minimizing the main window enters the mini view.
- [x] 5.3 Manually verify with a pre-upgrade settings file that had
      `MinimizeToTray = true`: after first launch the combo shows `Tray`,
      and minimizing still shows the tray icon as before.
- [x] 5.4 Manually verify with a pre-upgrade settings file that had
      `MinimizeToTray = false`: after first launch the combo shows
      `Mini View`.
