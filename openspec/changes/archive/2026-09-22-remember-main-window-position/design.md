## Context

See `proposal.md` - Why. Relevant existing code this design builds on:

- `MainWindow` already remembers `Width` via `Settings.MainWindowWidth`,
  restored in `RestoreWidth()` (`MainWindow.xaml.cs`) using
  `ScreenPlacement.ClampWidth`. `Height` is deliberately not persisted -
  `SizeToContent="Height"` derives it from the issue list.
- `MiniTimerWindow` already remembers its position the same way this change
  wants to for `MainWindow`: `Settings.MiniViewLocation` (an `"x,y"` string),
  parsed with `ScreenPlacement.TryParseLocation`, validated with
  `ScreenPlacement.EnsureOnScreen`, written back with
  `ScreenPlacement.FormatLocation`.
- `MainWindow` already has **transient** `Left`/`Top`/`WindowState` capture -
  `restoreLeft`, `restoreTop`, `restoreWindowState` - used only to return the
  window to where it was before entering the mini view or taskbar widget
  (`EnterMiniView`/`EnterTaskbarWidget` -> `RestorePosition()`). This is
  in-memory only and unrelated to cross-restart persistence, but it means two
  different notions of "remembered position" now coexist in the same class
  and must not be confused with each other.

## Goals / Non-Goals

**Goals:**

- Reuse `ScreenPlacement`'s existing geometry helpers as-is; add no new
  geometry math.
- Keep the persisted position/state orthogonal to the existing transient
  mini-view/taskbar-widget restore mechanism - one must not overwrite the
  other.

**Non-Goals:**

- No change to how `MainWindow` behaves while switching into or out of the
  mini view or taskbar widget - that transient restore logic is untouched.
- No change to `MiniTimerWindow`, `SettingsWindow`, `WorklogWindow`, or
  `EditTimeWindow` (see proposal.md - Impact).
- No new `ClampHeight` or similar - height stays derived, as today.

## Decisions

### D1. One setting, `MainWindowLocation` (string), for position

Mirrors `MiniViewLocation` exactly: `ScreenPlacement.FormatLocation`/
`TryParseLocation` round-trip an `"x,y"` string. Two separate int settings
(`MainWindowLeft`/`MainWindowTop`) were considered and rejected only because
it would diverge from the pattern `MiniViewLocation` already established for
no benefit - the string form is already what `ScreenPlacement` speaks.

### D2. Maximized state is a separate bool setting, `MainWindowMaximized`

`WindowState` has no geometry to validate (unlike position, it can't become
"unreachable" from a monitor change), so it doesn't belong inside the
`ScreenPlacement`-mediated location string. A plain `bool` persisted the same
way `AlwaysOnTop` already is needs no new helper.

Minimized is never persisted (see spec: "Minimizing does not change the
remembered state"): only `Maximized` (true) or `Normal` (false) are written.
If the window is minimized when the app closes, the save path uses whatever
`Normal`/`Maximized` value was in effect before the minimize, not the
minimized state itself - see D4.

### D3. Save triggers mirror the existing width pattern, not the transient one

**Superseded by D9** - kept for the record of what was tried first.

`MainWindow_SizeChanged` already writes `Settings.MainWindowWidth` on every
width change (guarded by `IsLoaded`). This change adds the same shape of
handler for position and state:

- `LocationChanged` -> write `Settings.MainWindowLocation` (guarded by
  `IsLoaded`, same as width, so the initial `Loaded`-time restore doesn't
  immediately overwrite itself).
- `StateChanged` -> write `Settings.MainWindowMaximized` only when the new
  state is `Normal` or `Maximized`; a transition to `Minimized` is ignored (no
  write), per D2/D4.

`MainWindow_Closed` needs no new explicit save call: by the time it runs, the
last `LocationChanged`/`StateChanged` write already reflects the window's
final on-screen state, exactly as already true for width today.

### D4. Reading position while minimized or in mini view/taskbar widget

**Superseded by D9** - kept for the record of what was tried first.

`Left`/`Top` continue to report the last normal/maximized geometry while
minimized (standard WPF/Win32 behavior - minimizing does not change `Left`/
`Top`, only `WindowState`), so `LocationChanged` while minimized is a
non-issue in practice. The one case that needs an explicit guard is the
existing mini-view/taskbar-widget round trip: `EnterMiniView`/
`EnterTaskbarWidget` already move the window off-screen or hide it before
capturing `restoreLeft`/`restoreTop`/`restoreWindowState` into the transient
fields. Because that repositioning happens through the same `Left`/`Top`/
`WindowState` properties, it would otherwise also fire `LocationChanged`/
`StateChanged` and persist a bogus position.

Resolution: the persistence handlers only run while the window is in its
normal, user-facing mode - i.e. skip the write whenever the mini view or
taskbar widget is currently active (the same condition `MainWindow` already
tracks to decide whether it's hidden for one of those views). This keeps the
new persistent settings and the existing transient restore mechanism from
D-Context fully decoupled, per the Goals above.

### D5. Restore order in `MainWindow_Loaded`

`WindowState` is set to `Normal` before restoring `Left`/`Top` (setting
`Left`/`Top` while `Maximized` is a no-op in WPF), then set to the remembered
`Maximized`/`Normal` value last, alongside the existing `RestoreWidth()` and
`ClampHeightToWorkingArea()` calls:

1. `RestoreWidth()` (existing, unchanged)
2. Restore `Left`/`Top`: parse `Settings.MainWindowLocation` with
   `ScreenPlacement.TryParseLocation`; on failure (empty/malformed/first run),
   keep the existing default placement untouched. On success, clamp with
   `ScreenPlacement.EnsureOnScreen(desired, new Size(ActualWidth or Width,
   ActualHeight or a reasonable estimate))` before assigning `Left`/`Top`.
3. `ClampHeightToWorkingArea()` (existing, unchanged)
4. Apply `Settings.MainWindowMaximized` to `WindowState`.

Doing the position restore before setting `WindowState = Maximized` means
`EnsureOnScreen` validates against the window's normal-state size, which is
the meaningful case - a maximized window's exact pixel geometry is decided by
Windows for whichever monitor it ends up on, not by the remembered `Left`/
`Top`.

### D6. `SizeToContent` is toggled off while maximized

Found during manual verification (task 4.2): setting `WindowState =
WindowState.Maximized` on a window with `SizeToContent="Height"` (as
`MainWindow` has) doesn't behave like an ordinary maximize. WPF re-applies
`SizeToContent`'s auto-fit after the maximize's layout pass, which shrinks the
window straight back down to its content size instead of leaving it filling
the screen - a WPF quirk with this property combination, not specific to this
window or to this change, just newly exercised by it because nothing
previously set `WindowState` to `Maximized` in code.

Fix: `MainWindow_StateChanged` sets `SizeToContent = SizeToContent.Manual`
in the `Maximized` branch and sets it back to `SizeToContent.Height` in the
`Normal` branch.

This was first suspected to also explain the width corruption from task 4.1,
but a clean-state retest (never maximized at all) still showed it - see D7 for
the actual cause.

**Follow-up, startup case only (found retesting after D9):** the fix above is
enough for an *interactive* maximize (the taskbar/chrome button, or
double-clicking the title bar): Windows fills the screen natively before
`StateChanged` ever fires, so the handler's job there is only to stop WPF's
next layout pass from shrinking it back down again. Restoring
`Settings.MainWindowMaximized = true` at startup goes through a *different*
path - `MainWindow_Loaded` sets `WindowState = Maximized` from code, with no
preceding native resize to piggyback on. There, WPF computes the maximized
target size in the same step as the `WindowState` assignment itself, still
using `SizeToContent="Height"` if that is not *already* `Manual` by the time
the assignment runs - reacting to it afterward, from `StateChanged`, is too
late for this path. Symptom: the window came up with `WindowState.Maximized`
chrome (taskbar, restore button) but at the remembered Normal-state size,
not filling the screen - clicking restore then correctly returned it to that
same Normal geometry, confirming `RestoreBounds` itself was fine and this was
purely a maximize-fill timing issue. Fix: `MainWindow_Loaded` sets
`SizeToContent = SizeToContent.Manual` itself, immediately before assigning
`WindowState = WindowState.Maximized`, rather than relying on the
`StateChanged` handler to react afterward. `StateChanged`'s own assignment
becomes a harmless no-op for this path (already `Manual` by then) and remains
the only one that matters for the interactive case.

Alternative considered: clamping `Height` manually instead of maximizing (i.e.
never actually set `WindowState.Maximized`, just resize to fill the working
area). Rejected: it would stop being a real OS-level maximize - no
double-click-titlebar-to-restore, no Aero Snap semantics, and the taskbar
icon's own restore/maximize affordances would desync from `WindowState`.
Toggling `SizeToContent` keeps `WindowState` meaning what it always means.

### D7. `RestoreWidth()` also runs in the constructor, before the window is ever shown

Found during manual verification (task 4.1, clean-state retest): even with
`WindowState` untouched (never maximized), a remembered width still didn't
stick - confirmed on disk (`user.config` correctly held the saved value) and
confirmed via the very first breakpoint hit inside `MainWindow_SizeChanged`,
which already reported the *wrong* width, before `MainWindow_Loaded`'s own
`RestoreWidth()` call had a chance to run.

Root cause: `SizeToContent="Height"` (see D-Context) makes WPF measure the
window's content to determine height. When `Width` has no explicit value yet
(true the first time the window is ever shown, since nothing sets it before
`Loaded`), that measure pass is unconstrained on *both* axes, not just the one
`SizeToContent` cares about - so it picks up the issue row template's own
natural, unconstrained width (all those `Auto` grid columns sized to their
content) as the window's initial `Width`, before `RestoreWidth()` in `Loaded`
ever gets a chance to constrain it. By the time `Loaded` runs and sets the
remembered width, the damage is already visible (and, worse, the resulting
`SizeChanged` from `RestoreWidth()`'s own assignment can itself get
double-counted against the wrong starting point).

Fix: call `RestoreWidth()` in the constructor too, right after
`InitializeComponent()` - before the window is ever shown, so `Width` already
holds a real value by the time WPF does its first `SizeToContent` measure
pass, and that pass only auto-sizes height as intended. `WorkingArea` at that
point falls back to the primary screen (its existing `!IsLoaded` branch - see
D-Context) since the window doesn't know its real target screen yet; the
existing call still in `Loaded` re-clamps against the actual screen once that
is known, same as before. This is a latent bug in the pre-existing width
persistence feature, not something introduced by this change - it just never
surfaced before because nothing had verified an exact remembered value against
the window's first-ever paint.

### D8. Width and location are only remembered while `WindowState == Normal`

Found during manual verification (task 4.2 retest, with D6/D7 already in
place): maximizing on a second monitor, closing, and reopening put the window
back on the *primary* monitor, not maximized, at exactly that monitor's
working width with a content-derived height. `user.config` showed why:
`MainWindowWidth` held a value matching a full screen width, and
`MainWindowLocation` held a small, near-origin pair typical of a maximized
window's native coordinates - neither looks like a width or position a user
would deliberately choose.

Root cause: `MainWindow_SizeChanged` (pre-existing) and
`MainWindow_LocationChanged` (D3) had no guard against `WindowState`. While
genuinely maximized - not hidden behind the mini view/taskbar widget, an
ordinary maximize - `ActualWidth` becomes the screen's width (`SizeToContent`
is `Manual` then, per D6) and `Left`/`Top` become whatever Windows reports for
a maximized top-level window (which does not track cleanly with monitor
working-area coordinates the way a Normal-state window's does). Both handlers
dutifully persisted that geometry as if the user had chosen it, overwriting
the real remembered Normal-state width/position. The `EnsureOnScreen` clamp
that expects a Normal-state, on-screen-monitor value doesn't recognize a
maximized-coordinates value as being on any monitor, so it falls back to the
primary screen - which is what actually produced "opens on the wrong
monitor."

This is the same category of mistake D2 already avoided for
`MainWindowMaximized` itself (only `Normal`/`Maximized` are meaningful values
to persist, `Minimized` is not) - it just wasn't applied to width/location
too. `WindowState`'s own value (D2/D3) is unaffected: it correctly recorded
`Maximized`, since that write is deliberately state-driven rather than
geometry-driven.

Fix (as first attempted - **superseded by D9**, see below): both handlers skip
whenever `WindowState != WindowState.Normal`, in addition to their existing
guards. What's remembered as "the" width/position is now exclusively the
geometry of the window's own Normal state, exactly the same concept as
`RestoreBounds` in Win32 terms - Maximized has no width/position of its own to
remember, only whether it applies to whatever Normal geometry is on file.

Consequence for anyone who already hit this bug before upgrading: the
`MainWindowWidth`/`MainWindowLocation` values already on disk may still hold
a stale maximized-geometry snapshot. This self-corrects the next time the
window is resized or moved while Normal - no migration needed, since nothing
distinguishes a "corrupted" value from an old legitimate one at the settings
layer.

### D9. Width/position are captured from `RestoreBounds` at save time, not from live events

Found during a follow-up retest of D8's fix, isolated to a clean two-step
repro (drag to a second monitor while Normal, close - correct; reopen,
maximize there, close - broke): `MainWindowLocation` ended up a few pixels
past that monitor's working-area corner (e.g. `-1928,-574` against a working
area starting at `-1920,-566` - an 8px offset in both axes, the invisible
resize border Windows includes in a maximized top-level window's reported
rectangle). D8's `WindowState != Normal` guard did not catch this.

Root cause: D8 assumed `LocationChanged`/`SizeChanged` observe a `WindowState`
that already reflects the transition in progress. For a native maximize
(clicking the window's own maximize button, not code setting `WindowState`),
Windows resizes/repositions the HWND first - which is what fires
`LocationChanged`/`SizeChanged` - and only then does WPF's `WindowState`
property update to `Maximized` and raise `StateChanged`. The first
`LocationChanged`/`SizeChanged` of a maximize therefore sees `WindowState`
still reporting `Normal`, so D8's guard let the maximized (and
border-inflated) geometry through anyway.

This ruled out chasing the event-ordering race further (trying to detect
"about to maximize" earlier is fragile and Windows-version-dependent) in
favor of not depending on live event timing at all. WPF's `Window.RestoreBounds`
is a thin wrapper over Win32's own `WINDOWPLACEMENT.rcNormalPosition` - the
"where this window would be if it were Normal" rectangle Windows itself
maintains continuously, correctly excluding the maximized-border overshoot,
and valid to read regardless of the window's current state (Normal, Maximized
or Minimized) or event-ordering quirks around any particular transition.

Fix: drop the `LocationChanged`/`SizeChanged` handlers (and their
`WindowState`/`inMiniView`/`inTaskbarWidget` guards from D3/D4/D8) entirely.
Add `RememberNormalGeometry()`, which reads `RestoreBounds.Width`/`.Left`/
`.Top` (converted to device pixels via the existing `GetScale()`, same as
before) into `Settings.MainWindowWidth`/`MainWindowLocation`, called from
`SaveSettingsAndIssueStates()` - the same place `Settings.Save()` already
runs from (every ticker tick, and on close), so the actual save cadence is
unchanged from D3's. This also drops the need for D4's mini-view/taskbar-widget
guard: `RestoreBounds` is untouched by those views hiding the window (they
never move or resize it), so there is nothing left to guard against.

**Immediate follow-up bug, same root idea, caught before this was ever
considered done:** `SaveSettingsAndIssueStates()` is called from
`MainWindow_Closed`, but `Closed` fires *after* the window's HWND is already
torn down - `RestoreBounds` had nothing left to query there and (via its
`IsEmpty` guard) silently skipped the save, so a resize/move immediately
before closing was lost entirely; the settings kept whatever the last ticker
tick (up to 30s earlier) or an older session had. Fix: capture via a new
`MainWindow_Closing` handler instead (fires while the window, and its HWND,
are still fully alive), calling `RememberNormalGeometry()` there directly.
`SaveSettingsAndIssueStates()` keeps its own call for the periodic ticker
path; the `Closed`-time call is now redundant for geometry (harmless no-op,
since `Closing` already captured it) but still needed for `issues.Persist()`
and `settings.Save()` itself.

## Risks / Trade-offs

- **Interaction with mini view/taskbar widget's transient restore (D-Context)
  is easy to get subtly wrong** if the persistence handlers aren't properly
  guarded per D4, e.g. persisting the off-screen position `EnterMiniView`
  moves the window to. Mitigated by D4's explicit guard and by testing the
  mini-view/taskbar-widget round trip alongside a normal restart in the same
  pass.
- **No automated UI test coverage for window chrome behavior** (WPF
  `Left`/`Top`/`WindowState` changes are awkward to unit test without a real
  window). `ScreenPlacement`'s pure functions are already unit-tested and
  unchanged by this design; the new wiring in `MainWindow.xaml.cs` is verified
  manually per the spec's scenarios, consistent with how the width feature and
  the mini view's position feature were verified before it.
