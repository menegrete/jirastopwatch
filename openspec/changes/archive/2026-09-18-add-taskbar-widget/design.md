## Context

See [proposal.md](proposal.md) for motivation (GitHub issue #14). The
starting point for the implementation approach is a working reference
implementation cloned locally at `C:\WC\jira\now-playing-taskbar-widget`
(MIT-licensed, `mechanicwb2-hub/now-playing-taskbar-widget`) that solves the
same underlying problem — a widget docked over the empty area of the
Windows taskbar — for Spotify. Modern Windows has no supported "deskband"
API, so that project (and this design) fake it: a borderless, topmost WPF
window is positioned by hand over the taskbar's free space and kept in sync
with it via Win32 interop. This design adapts its techniques (anchor
discovery, auto-hide riding, ownership, multi-monitor identity) to
JiraStopwatch's much simpler content (one text row, no media transport) and
existing architecture.

JiraStopwatch already has the two building blocks this reuses:
- `ActiveTimerViewModel` resolves "what issue should a compact display
  show" — the taskbar widget is a second consumer, alongside
  `MiniTimerWindow`.
- `Helpers/ScreenPlacement.cs` establishes the pattern of pure,
  DPI-agnostic, unit-tested geometry functions kept separate from the
  `System.Windows.Forms.Screen`/WPF interop that calls them. The taskbar
  widget's geometry (which side has free space, where the visible band of
  the taskbar band is, how far into a slide animation the taskbar currently
  is) follows the same split.

## Goals / Non-Goals

**Goals:**
- Match the reference implementation's fidelity: real anchor discovery via
  UI Automation, real auto-hide riding via `WinEventHook` +
  `SetWindowRgn`, real multi-monitor support with stable per-monitor
  identity, real window ownership via `GWLP_HWNDPARENT`.
- Keep all raw P/Invoke in one interop module, isolated from the pure
  placement math and from the WPF window's event handling, mirroring
  `ScreenPlacement.cs`'s split.
- Reuse `ActiveTimerViewModel` and existing single-timer-rule/settings
  plumbing unchanged.

**Non-Goals:**
- No media-style responsive button ladder — the widget has one control
  (pause/resume) and two pieces of text (key, summary), not the six
  optional buttons the reference project juggles. "Not enough space" only
  ever means "drop the summary," never "drop the pause button."
- No packaging/store-specific concerns (`PackagedApp.cs` in the reference)
  — JiraStopwatch already has its own auto-update mechanism
  (`add-auto-update`, archived), unrelated to this feature.
- No support for Windows versions/shells where `WidgetsButton`/`StartButton`
  automation IDs don't exist (very old Win10 builds without the Win11
  taskbar). Anchor discovery failing there degrades to the "no hueco
  suficiente → esconderse" scenario already in the spec, not a crash.

## Decisions

### Window ownership and lifecycle

`TaskbarWidgetWindow` sets `GWLP_HWNDPARENT` to the target `Shell_TrayWnd`/
`Shell_SecondaryTrayWnd` handle, exactly like the reference. This makes the
window manager keep it topmost relative to the taskbar without a per-frame
z-order fight, and — because it's owned by the taskbar — it dies for free
if `explorer.exe` restarts. `MainWindow` listens for that close and
recreates the widget the same way `EnterMiniView`/`ExitMiniView` already
toggle `MiniTimerWindow` today, so a transient Explorer restart looks like a
brief flicker, not a stuck state.

**Alternative considered**: polling `IsWindow(trayHwnd)` and manually
re-asserting topmost every tick, as a plain always-on-top window would.
Rejected — it's what causes the z-order flicker the reference's comments
explicitly call out fixing by switching to `GWLP_HWNDPARENT`.

### Anchor discovery (where the free space is)

A new `Helpers/TaskbarAnchors.cs`, closely modeled on the reference's file
of the same name, uses `System.Windows.Automation` to find the
`AutomationId`s `WidgetsButton` and `StartButton` under the target tray
window, plus the rightmost `Taskbar.TaskListButton`. These three points
bound the free space regardless of whether the taskbar is centered (Win11)
or left-aligned (Win10/classic Win11): centered taskbars have free space on
the left of the widgets button; left-aligned taskbars have free space before
the notification area (`TrayNotifyWnd`, found via `FindWindowEx`).

Queries run off the UI thread, throttled (every few seconds, matching the
reference's 5s), and a *failed* query (UIA threw) keeps the previous anchors
instead of treating the failure as "the button is gone" — this is what
stops the widget from jumping onto the clock button during a transient UIA
hiccup, a failure mode the reference's own comments describe hitting.

**Alternative considered**: pixel-scanning the taskbar bitmap for empty
space. Rejected — fragile across themes/DPI and exactly the kind of
approach UI Automation exists to avoid.

### Auto-hide riding

`Interop.IsAutoHideEnabled()` wraps `SHAppBarMessage(ABM_GETSTATE)`. A
`WinEventHook` on `EVENT_OBJECT_LOCATIONCHANGE`, scoped to the target
taskbar's own thread/process, fires on every step of the taskbar's
show/hide animation; the widget's Y position and a `SetWindowRgn` clip are
recomputed on each callback so it rides the same animation instead of
teleporting once the taskbar settles. This is copied close to verbatim from
the reference's `MainWindow.UpdatePosition`/`StartRide`/`Interop.
ClipWindowBottom`, adapted to a single fixed-height row instead of a
variable-height card.

**Alternative considered**: a fixed-interval timer (e.g. every 100ms)
polling the taskbar's rect and jumping the widget to match. Rejected —
visibly stutters against the real animation; the event-driven approach the
reference uses is what makes the ride look native.

### Multi-monitor identity

`Interop.GetSecondaryTrays()` enumerates `Shell_SecondaryTrayWnd` windows
and sorts them by monitor position (left→top), not enumeration/z-order,
because z-order changes constantly and would make "monitor 2" silently swap
identity with "monitor 3" between sessions — a bug the reference's own
comments describe having hit. `Settings` gains a persisted list of chosen
monitor indices (0 = primary); one `TaskbarWidgetWindow` instance is created
per selected index that currently resolves to a connected tray, mirroring
`MainWindow.SyncToMonitors`. If none of the chosen monitors are connected,
one window falls back to the primary tray (per the spec's "monitor
principal" scenario) — the reference's fuller "lowest orphan" tie-breaking
across many windows is unnecessary here since JiraStopwatch only ever shows
one active issue, not per-monitor independent content.

### Where the new code lives

- `Helpers/TaskbarInterop.cs` — all P/Invoke (`SHAppBarMessage`,
  `SetWindowRgn`, `SetWinEventHook`, `GetWindowRect`,
  `GetMonitorInfo`, `SetWindowLongPtr`/`GWLP_HWNDPARENT`,
  `GetForegroundWindow` + fullscreen check). No WPF types.
- `Helpers/TaskbarAnchors.cs` — UI Automation lookups only.
- `Helpers/TaskbarPlacement.cs` — pure functions (given taskbar rect,
  anchors, DPI, window size → target position; given visible-pixels state →
  animation phase), unit-tested the same way `ScreenPlacementTest` covers
  `ScreenPlacement.cs`, without a real screen or a real taskbar.
- `UI/TaskbarWidgetWindow.xaml(.cs)` — the WPF window: one row (reusing the
  same row visuals `MiniTimerWindow`'s single-row layout already uses),
  pause/resume button, context menu, double-click restore, driven by
  `ActiveTimerViewModel` the same way `MiniTimerWindow` is.

This keeps the same shape the codebase already uses for
`MiniTimerWindow`/`ScreenPlacement.cs`: dirty Win32 interop and pure testable
math in separate files, WPF event wiring in the window's code-behind.

### Settings shape

`MinimizeBehavior` gets a third enum value (`TaskbarWidget = 2`), following
the exact pattern already in `Settings.cs`/`Properties/Settings.settings`
for the existing `MiniView`/`Tray` values — no new migration path needed
beyond what already exists, since old installs don't have this value to
migrate from.

Chosen monitor indices are stored as a comma-separated string in a new
`Properties.Settings` entry (`TaskbarWidgetMonitors`), parsed/formatted in
`Settings.cs`, consistent with how other simple list-like values are
persisted in this project's hand-written `Settings.cs` wrapper (no need for
the reference's separate JSON settings file — JiraStopwatch already has one
settings persistence mechanism and this fits it).

The `AllowMultipleTimers` ⇄ `TaskbarWidget` mutual exclusion
(`minimize-behavior` delta spec) is enforced only in `SettingsWindow`'s UI
(disabling the other control) — no new model-layer validation, since
`IssueListViewModel`'s single-timer rule already assumes
`AllowMultipleTimers == false` behaves correctly regardless of which
minimize behavior is active.

## Risks / Trade-offs

- **UI Automation against Explorer is inherently version-fragile.**
  `WidgetsButton`/`StartButton` are Win11 taskbar internals, not a public
  contract — a Windows update could rename or restructure them. →
  Mitigation: anchor queries fail closed (keep last known anchors, or hide
  when none have ever been read, per the spec's scenarios), never crash the
  app; this is the same trade-off the reference project already ships with
  in production.
- **`SetWindowRgn`/`WinEventHook`/UI Automation are Windows-only, unmanaged,
  and easy to leak or misuse.** → Mitigation: isolate every P/Invoke call in
  `TaskbarInterop.cs`, unhook on window close (mirroring the reference's
  `Closed` handler), and keep the feature reachable only when
  `CrossPlatformHelpers.IsWindowsEnvironment()` is true, exactly like the
  rest of `minimize-behavior` already is.
- **Explorer restarting (crash, manual restart, some updates) kills the
  taskbar's window.** → Mitigation: covered above via
  `GWLP_HWNDPARENT` ownership + recreate-on-close, not a special case to
  handle separately.
- **Reusing techniques from an MIT-licensed project inside an
  Apache-2.0-licensed one.** → Mitigation: this design adapts the
  *technique*, not a line-for-line port; where code ends up closely
  mirroring the reference (auto-hide riding math in particular), keep the
  MIT notice for that file's origin alongside the project's usual
  Apache-2.0 header, per MIT's attribution requirement.
