# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Build and test the whole solution:

```
dotnet build StopWatch.sln
dotnet test StopWatch.sln --settings .runsettings
```

Run a single test (NUnit, by fully-qualified name or a filter):

```
dotnet test StopWatch.sln --filter "FullyQualifiedName~ScreenPlacementTest.EnsureOnScreen"
```

Produce a release build (framework-dependent single file, requires the .NET 10 Desktop Runtime on the target machine):

```
dotnet publish source/StopWatch/StopWatch.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

`TreatWarningsAsErrors` is on for `StopWatch.csproj` — a warning fails the build, not just `Release`.

## Architecture

Two UI frameworks in one process: `UseWindowsForms` and `UseWPF` are both `true` on `net10.0-windows`. WinForms stays only for what WPF has no equivalent for — the tray icon (`NotifyIcon`) and screen enumeration (`System.Windows.Forms.Screen`, used by `Helpers/ScreenPlacement.cs`). Everything with a visible window (`MainWindow`, `MiniTimerWindow`, `SettingsWindow`, `WorklogWindow`, `EditTimeWindow`) is WPF. This split is deliberate and permanent — see the OpenSpec design for `migrate-ui-to-wpf` under `openspec/changes/archive/` before trying to remove either framework.

**Model/ViewModel layer** (`source/StopWatch/Model/`) is UI-framework-agnostic and unit-tested independently of both WinForms and WPF:
- `IssueListViewModel` owns the collection of issue rows and the single-timer rule: starting one timer pauses all others unless `Settings.AllowMultipleTimers` is set (`PauseAllBut`, driven by each row's `TimerStarted` event).
- `IssueViewModel` is one row: Jira issue data plus its `WatchTimer`.
- `ActiveTimerViewModel` answers "which issue should a compact display show" without callers walking the row collection themselves. It resolves the active issue (the one running; the last one started if several are; the last one that ran if none are; otherwise the row selected in the main window) and exposes `RunningSources` for anything that needs the full set of running rows. It takes a `Func<IEnumerable<ITimerSource>>` rather than depending on `IssueListViewModel` directly, so it can be unit-tested with fakes.
- `WatchTimer` is the actual elapsed-time/running-state primitive each row wraps.

**Jira communication** (`source/StopWatch/Jira/` and `Model/IssueJiraService.cs`) goes through `IJiraOperations` → `JiraClient` → `IJiraApiRequester`/`IJiraApiRequestFactory`, built on RestSharp. The indirection exists so tests can substitute `IJiraOperations`/`IJiraApiRequester` with Moq instead of hitting a real Jira instance.

**Settings** (`source/StopWatch/Settings/Settings.cs`) is a hand-written wrapper around the generated `Properties.Settings.Default` — add a new setting in `Properties/Settings.settings` (regenerates `Settings.Designer.cs`), then read/write it in `Settings.cs`'s `ReadSettings`/`Save`, following the existing properties. `PersistedIssue` is the separate JSON-serialized blob for the issue list itself, not part of `Properties.Settings`.

**The mini timer view** (`UI/MiniTimerWindow.xaml(.cs)`) is a separate always-on-top WPF window fed entirely by `ActiveTimerViewModel`; `MainWindow` hides itself while it's up. It runs its own 1-second `DispatcherTimer` to refresh the displayed elapsed time — `MainWindow`'s own ticker stays at 30 seconds because it's doing Jira round-trips, and the model itself has no timer of its own (`Refresh()` is called by whoever is displaying it, at whatever rate that display needs). Positioning code deliberately juggles two coordinate systems: `Screen.WorkingArea` is device pixels, WPF's `Left`/`Top`/`Width`/`Height` are device-independent units; `Helpers/ScreenPlacement.cs` holds the screen-geometry math (edge-snapping, clamping a remembered position back onto a currently-connected screen) as pure, DPI-agnostic functions so it's testable without a second monitor.

**Theming** (`UI/Theme.cs`, `ThemeBrushes.cs`) is a semantic palette (`Background`, `Surface`, `Text`, `TimerRunning`, etc.) that both the WinForms tray icon and the WPF windows read from, so there's one source of truth for colors across both frameworks.

## Planning workflow

This repo uses OpenSpec (`openspec/`) for planning non-trivial changes: `openspec/specs/` holds the current behavior contract per capability, `openspec/changes/` holds in-flight change proposals (proposal/specs-delta/design/tasks), and `openspec/changes/archive/` holds completed ones. When a past design decision or its rationale isn't obvious from the code, check the relevant archived change there before re-deriving it.

## Changelog

`CHANGELOG.md` (Keep a Changelog format, keyed by the version in `source/StopWatch/Properties/AssemblyInfo.cs`) must stay current with the code. Whenever you make a code change with user-visible effect (a feature, a fix, a behavior change — not a pure refactor, test-only change, or internal cleanup), add or update its entry under `## [Unreleased]` in the same turn, in the appropriate Keep a Changelog category (`Added`, `Changed`, `Fixed`, `Removed`, etc.). Do this without waiting to be asked. When `AssemblyInfo.cs`'s version is bumped for a release, rename `[Unreleased]` to that version (with the date) and start a fresh empty `[Unreleased]` above it.
