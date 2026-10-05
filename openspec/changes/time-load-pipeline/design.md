# Design

## Context

See `proposal.md` for the motivation and the specs for the observable behavior. This document covers how it is built.

What the code looks like today and constrains the approach:

- `MainWindow.PostWorklogAsync` is the only caller of `IssueJiraService.PostWorklogAsync`. It sets a wait cursor for the whole call, re-reads `issue.WatchTimer.TimeElapsedNearestMinute` after the dialog closes, and resets the timer if `Success`. `WorklogWindow` does not let the user edit the total: the "confirmed" total is the elapsed time the host reads at that moment.
- `AppComposition` builds the `IssueJiraService`, the `IssueListViewModel` and the `ActiveTimerViewModel`, and `PluginManager.Load` receives it to wire the plugin adapters. Each plugin gets **its own** `PluginHost` instance, created by a factory that already knows the plugin id. Plugins are loaded after the main window exists.
- `PluginJiraApi` is the single `IJiraApi` shared by all plugins. Its methods report failure with `null`/`false`, and `AddWorklogAsync` always sends `EstimateUpdateMethods.Auto`.
- `IJiraOperations` already exposes `GetSubtasks`, `GetSubtaskTypes` and `CreateSubtask(parentKey, summary, issueTypeId)` (change `add-jira-search-subtasks`). Nothing in the plugin contract reaches them.
- There is no non-modal notice mechanism in the UI. `ShowBalloonTip` is not used anywhere; the status bar only has a connection label and the update-ready label.
- Async UI calls are launched with `FireAndForget()`, which reports unobserved exceptions through `App.ReportException`.

## Goals / Non-Goals

**Goals:**
- One pipeline in `Model/`, UI-agnostic, unit-testable with fakes, whose outcome for every combination of handler answer and write count is a function of its inputs only.
- No behavior change when no plugin takes part.
- Failure of a plugin never loses time silently and never loads the same time twice when it can be avoided.

**Non-Goals:**
- Verifying what a plugin actually wrote beyond its declared `TimeLoaded` and the write count.
- Partial loads on purpose, a partial timer, or any way for a plugin to keep the timer from resetting.
- Changing `WorklogWindow`.

## Decisions

### 1. `TimeLoadPipeline` in `Model/`, collaborators injected

`TimeLoadPipeline` is an internal class constructed with: the original load (`IssueJiraService`), a registry of handlers and observers, a factory for the per-invocation scoped Jira API, a notifier for visible notices, and a log delegate. It has no WPF types. `AppComposition` builds one instance; `MainWindow` and the plugin adapters both receive it from there.

*Alternative:* put the stages inside `IssueJiraService`. Rejected: that class posts to Jira and knows nothing of plugins, and the issue asks for the `IssueJiraService` pattern, not its extension.

### 2. Contract types live in Abstractions, with factories instead of an enum

The handler answers with a class (`InsteadOfResult`) built only through static factories: `Declined()`, `Handled(TimeSpan timeLoaded)`, `Cancelled()`, `Failed(string reason = null)`. It is a class so that `Handled` can carry data and `Failed` an optional reason without a type per answer. The request (`TimeLoadRequest`) carries the confirmed values and a `TimeLoadSource` (a small class: user, or plugin with an id) rather than a parsed string. The `After` notification (`TimeLoadedEventArgs`) carries the request values, who handled it (host or plugin id), the outcome (`Succeeded`, `Failed`, `Cancelled`), the reason and the write count. Estimates cross the boundary as a contract enum that mirrors `EstimateUpdateMethods`; the host enum is never exposed.

### 3. Registration through the per-plugin host, not .NET events

`IPluginHost` gains `TimeLoad` (register the one `InsteadOf` handler, subscribe to `After`) and `TimeLoader` (`ITimeLoader`). The `PluginHost` factory already knows the id, so a per-plugin `PluginTimeLoad` created with that id registers `(id, handler)` in the shared registry. A second registration from the same plugin throws, which the loader isolates as an initialization failure. `After` is a regular event: multicast is fine because it returns nothing, and the pipeline invokes each subscriber separately so one throwing does not stop the rest.

*Alternative:* `event Func<…, Task<InsteadOfResult>>`. Rejected: a multicast delegate only returns its last value and cannot carry the plugin id.

### 4. Outcome is a pure function of (answer, writes, declared time)

The pipeline reduces one consulted handler to an outcome, without touching UI or Jira:

| Answer | Writes | Declared vs confirmed | Outcome |
|---|---|---|---|
| Declined | 0 | – | Run original |
| Declined | >0 | – | Failed with writes |
| Handled | any | equal | Succeeded, reset timer |
| Handled | any | different | Partial: no reset, notice |
| Cancelled | 0 | – | Nothing |
| Cancelled | >0 | – | Failed with writes (a cancel after writing is not a clean cancel) |
| Failed / exception | 0 | – | Fallback to original, visible |
| Failed / exception | >0 | – | Failed with writes: no fallback, no reset |

The result object returned to `MainWindow` says only: `ResetTimer`, whether a notice is needed and its text. Tests assert on this table row by row, with a fake handler, a fake original and a fake notifier. The row *Cancelled with writes* is not in the issue; it follows the same logic as *Declined with writes* and closes a gap in the table.

### 5. Validation of `TimeLoaded` is against the confirmed total only

`Handled` is valid when the declared `TimeLoaded` equals the total the host passed in the request (whole minutes, as `TimeElapsedNearestMinute` already is). It is not cross-checked against the scoped API's counter, which counts writes, not time. Cross-checking would need the counter to sum time and would still trust the plugin about which writes were meant to cover the load.

*Trade-off:* a plugin can declare the right total and write something else. The host does not claim to catch that.

### 6. The scoped Jira API is a counting decorator, passed in the request

For each `InsteadOf` invocation the pipeline builds a fresh decorator over the shared `PluginJiraApi`. It counts a write only when the underlying call reports success (`AddWorklogAsync` true, `CreateSubtaskAsync` non-null key). It is per invocation, so concurrent loads never share a counter.

*Rejected alternative:* an ambient counter (`AsyncLocal`) so that `host.Jira` also counts. It would catch plugins that ignore `request.Jira`, but it is implicit, leaks into background work the plugin starts, and is hard to test. The cost is a documented footgun: writes through `host.Jira` inside a handler are not counted, so a later failure would fall back and may load the time twice. `docs/plugins.md` and the `SplitTime` sample say to use `request.Jira` inside the handler.

### 7. Threading: handlers and observers run on the UI thread

Handlers may open WPF windows. `MainWindow` starts the pipeline from the UI thread and the pipeline awaits handlers without `ConfigureAwait(false)` before them, so they run on the dispatcher. Calling the handler is wrapped in `try/catch` that also covers a throw before the first `await` and a `null` task or result, both treated as `Failed`. The write path itself (`PluginJiraApi`) keeps using `Task.Run` as today, so a handler awaiting it does not block the UI.

### 8. Busy cursor only around host-owned work

`MainWindow.PostWorklogAsync` stops setting the wait cursor for the whole call. The pipeline takes a busy callback and raises it only around the original load, so the cursor is not stuck on `Wait` while a plugin dialog waits for a person.

### 9. One load per issue at a time

`MainWindow` keeps a set of issues with a load in progress and ignores a new post request for the same issue until it finishes. Without it, with no handler timeout and `FireAndForget`, the user could start a second load of the same time while the first one waits in a plugin dialog. A silent ignore is enough: the first load's dialog is already on screen.

### 10. A load started from inside a handler does not consult handlers again

`ITimeLoader.LoadAsync` called while a replacement handler is running (tracked with an `AsyncLocal` flag set by the pipeline for the handler's duration) skips the replacement stage: the original load and the observers still run. This prevents `A → B → A` recursion. A load a plugin starts outside a handler, from a command for example, goes through the full pipeline, with its own handler excluded.

### 11. Notices: status bar plus log, tray balloon when the window is hidden

The notifier writes a message to a new label in the status bar, themed with the existing brushes, kept until the user clicks it or the next load starts, and logs the same text. When the main window is not visible (the mini timer view hides it), it also raises a tray balloon. A modal `MessageBox` is not used: it contradicts the non-modal requirement and blocks the thing the user is trying to finish.

### 12. Contract 1.1 surface

`IJiraApi` gains: an `AddWorklogAsync` overload taking the estimate method and value; `GetSubtasksAsync(parentKey)` returning public descriptions (key, summary) or `null` on failure; `CreateSubtaskAsync(parentKey, summary)` returning the new key or `null`. The host resolves the issue type id itself: it asks `GetSubtaskTypes` for the parent's project and uses the first type, failing if the project offers none. `PluginContract.ContractVersion` and the Abstractions `<Version>` move to 1.1; the existing compatibility check already makes 1.0 plugins load on a 1.1 host and the reverse not.

### 13. The timer is reset after a possibly long interaction

The elapsed time is read once, when the dialog closes, and carried in the request, as today. If a plugin dialog takes minutes while the timer runs, the reset at the end also discards the time accrued meanwhile. This is the same behavior the app has with a slow Jira request, only longer. Fixing it would mean a partial timer, which is out of scope.

## Risks / Trade-offs

- [Plugin writes through `host.Jira` instead of `request.Jira`] → Not counted, so a failure falls back and may double-load. Documented; the sample follows the rule. Detectable only by convention.
- [Plugin declares the right `TimeLoaded` but wrote something else] → Not caught. The host validates the declaration, not the writes.
- [Retry after a partial failure] → The timer is not reset, so the user retries with the same button and the plugin sees the same load again. The plugin must be idempotent (key, start and elapsed hash stored in its data directory); the host promises nothing about it.
- [Time accrued during a long plugin dialog is lost on reset] → See decision 13; documented.
- [Status bar notice is missed] → Kept until dismissed, plus the log, plus a tray balloon when the window is hidden.
- [`IPluginHost` gains members] → Plugins consume it, they do not implement it, so it is additive for them.

## Migration Plan

No data or settings migration. Existing 1.0 plugins keep working. Rolling back is removing the pipeline wiring in `MainWindow` and returning to the direct `IssueJiraService` call; the Abstractions additions are harmless if left.

## Open Questions

- Wording and styling of the status bar notice, and whether the click-to-dismiss behavior is enough. It does not affect the specs or the task breakdown.
