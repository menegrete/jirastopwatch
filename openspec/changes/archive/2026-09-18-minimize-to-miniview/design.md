## Context

See proposal.md - Why. Today `Settings.MinimizeToTray` is a plain bool,
generated from a primitive entry in `Properties/Settings.settings`, wrapped
by hand in `Settings.cs`. `MainWindow_StateChanged` reads it directly:

```
if (inMiniView) return;
if (!settings.MinimizeToTray) return;
if (WindowState == Minimized) { ShowTrayIcon(); Hide(); }
else if (WindowState == Normal) { HideTrayIcon(); }
```

`EnterMiniView()`/`ExitMiniView()` (the toolbar button's path) already save
and restore `WindowState`, `Left`/`Top`/`Width`, and already coerce a
remembered `Minimized` back to `Normal` on restore — that coercion exists
precisely because entering mini view can happen from any `WindowState`.

## Goals / Non-Goals

**Goals:**
- Replace the bool setting with a two-valued choice (`MiniView` / `Tray`)
  that is impossible to leave in an invalid combination.
- Route the native minimize gesture to the right existing mechanism
  (`EnterMiniView()` or the current tray path) based on that choice.
- Carry existing users' preference forward without asking them to
  re-configure anything.

**Non-Goals:**
- Changing how `EnterMiniView()`/`ExitMiniView()` work internally — reused
  as-is.
- Adding a third "plain taskbar minimize" option — the proposal removes it.
- Changing the toolbar mini-view button, or persisting mini-view state
  across restarts — both stay exactly as they are today.

## Decisions

**1. New setting is an `Int32`-backed enum, matching the codebase's existing
convention for enum settings.** `Theme`, `SaveTimerState`,
`PauseOnSessionLock`, `PostWorklogComment` and `ListDensity` are all
`System.Int32` entries in `Properties/Settings.settings`, cast to/from a C#
enum in `Settings.cs` (`ReadSettings`/`Save`) — the designer has no native
enum type, and this is the established way around that, not a string. The
new entry (`MinimizeBehavior`, default `0`) follows the same shape, backing
`enum MinimizeBehavior { MiniView, Tray }`.
*Alternative considered*: a string-backed setting (`"MiniView"` / `"Tray"`)
so an empty string could double as an "unmigrated" sentinel. Rejected in
favor of matching the established `Int32` convention; see Decision 2 for how
migration is gated without relying on the setting's own value.
*Alternative also considered*: add a second bool (`MinimizeToMiniView`)
alongside the existing one. Rejected — it re-opens exactly the invalid state
space (both true, both false) the combo exists to remove.

**2. Migration is gated by a dedicated sentinel, not by inspecting
`MinimizeBehavior`'s value.** Unlike a string, `Int32` has no "unset" value
distinguishable from a real one — `0` is both `MinimizeBehavior.MiniView`
and the type's default, so it can't tell "never migrated" apart from "the
user (or a fresh install) chose MiniView". A new bool setting,
`MinimizeBehaviorMigrated` (default `False`), tracks this directly: on
`ReadSettings`, if it's `False`, set `MinimizeBehavior` from `MinimizeToTray`
once (`true` → `Tray`, `false` → `MiniView`), set
`MinimizeBehaviorMigrated = True`, and persist both. A fresh install with no
prior `MinimizeToTray` also has `MinimizeToTray = false` (its own declared
default), so this same one-time pass correctly lands it on `MiniView` too —
there's no separate "new install" code path. Once migrated, `MinimizeToTray`
is never read again; it stays in `Settings.settings` untouched (see Risks).
This sentinel is a pure implementation detail: it's not surfaced in
Configuración and not part of any spec.
*Alternative considered*: keep consulting `MinimizeToTray` live at every
minimize instead of migrating once. Rejected — leaves two settings
permanently in play, contradicts "the combo replaces the checkbox" from the
proposal, and would silently revert a user's later choice back to whatever
`MinimizeToTray` happened to hold.

**3. `MainWindow_StateChanged` branches on the new enum instead of the old
bool.** `MiniView` calls `EnterMiniView()` — the same entry point the
toolbar button already uses, no new hide/show logic. `Tray` keeps today's
`ShowTrayIcon(); Hide();`. The existing `if (inMiniView) return;` guard at
the top of the handler is untouched, so minimizing while already in mini
view stays a no-op there, same as today.

**4. No new handling needed for `WindowState` already being `Minimized`
when `EnterMiniView()` runs.** WPF flips `WindowState` to `Minimized` before
`StateChanged` fires, so `EnterMiniView()` would capture `Minimized` into
`restoreWindowState` — but `ExitMiniView()` already coerces a remembered
`Minimized` back to `Normal` on restore (pre-existing behavior from
`add-mini-timer-view`, written for the toolbar-button path but equally
correct here). Reused as-is, no change required.

## Risks / Trade-offs

- [Risk] Users who relied on plain "minimize to taskbar, nothing special"
  lose that option entirely — there's no third combo value.
  → Mitigation: none needed technically; this is the proposal's intended,
  explicitly **BREAKING** behavior change. Noted here only so it isn't
  "fixed" by accident during implementation.
- [Risk] Re-running the migration on every startup instead of once could
  silently overwrite a user's later choice back to the migrated value.
  → Mitigation: gate migration strictly on "`MinimizeBehavior` has no
  persisted value yet" (Decision 2), not on `MinimizeToTray`'s value.
- [Risk] Downgrading the exe after this ships would run old code that still
  reads `MinimizeToTray` directly.
  → Mitigation: covered by Decision 2 keeping `MinimizeToTray` in
  `Settings.settings` and never overwriting it — a downgrade sees its
  original historical value and behaves as it did before the upgrade.
