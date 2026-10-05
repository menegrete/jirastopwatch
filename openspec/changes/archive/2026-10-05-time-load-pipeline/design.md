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
- After a partial load, the timer holds only what is not loaded, so a retry needs no help from the plugin to avoid loading the same time again.

**Non-Goals:**
- Verifying what a plugin actually wrote beyond its declared `TimeLoaded`, the write count and the time of the worklogs it posted through `request.Jira`.
- Partial loads on purpose, or any way for a plugin to keep the timer from resetting or to choose how much is subtracted.
- Changing `WorklogWindow`.

## Decisions

### 1. `TimeLoadPipeline` in `Model/`, collaborators injected

`TimeLoadPipeline` is an internal class constructed with: the original load (`IssueJiraService`), a registry of handlers and observers, the shared Jira API from which it builds the per-invocation scoped one, and a log delegate. It has no WPF types. `AppComposition` builds one instance; `MainWindow` and the plugin adapters both receive it from there. The notifier for user notices is a property, not a constructor argument: the pipeline is built in `AppComposition` before the main window exists, and `MainWindow` assigns the notifier when it is created. Left unassigned, notices are only logged.

*Alternative:* put the stages inside `IssueJiraService`. Rejected: that class posts to Jira and knows nothing of plugins, and the issue asks for the `IssueJiraService` pattern, not its extension.

### 2. Contract types live in Abstractions, with factories instead of an enum

The handler answers with a class (`InsteadOfResult`) built only through static factories: `Declined()`, `Handled(TimeSpan timeLoaded)`, `Cancelled()`, `Failed(string reason = null)`. It is a class so that `Handled` can carry data and `Failed` an optional reason without a type per answer. The request (`TimeLoadRequest`) carries the confirmed values and a `TimeLoadSource` (a small class: user, or plugin with an id) rather than a parsed string. The `After` notification (`TimeLoadedEventArgs`) carries the request values, who handled it (host or plugin id), the outcome (`Succeeded`, `Failed`, `Cancelled`), the reason and the write count. Estimates cross the boundary as a contract enum that mirrors `EstimateUpdateMethods`; the host enum is never exposed.

### 3. Registration through the per-plugin host, not .NET events

`IPluginHost` gains `TimeLoad` (register the one `InsteadOf` handler, subscribe to `After`) and `TimeLoader` (`ITimeLoader`). The `PluginHost` factory already knows the id, so a per-plugin `PluginTimeLoad` created with that id registers `(id, handler)` in the shared registry. A second registration from the same plugin throws, which the loader isolates as an initialization failure. `After` is a regular event: multicast is fine because it returns nothing, and the pipeline invokes each subscriber separately so one throwing does not stop the rest.

*Alternative:* `event Func<…, Task<InsteadOfResult>>`. Rejected: a multicast delegate only returns its last value and cannot carry the plugin id.

### 4. Outcome is a pure function of (answer, writes, measured time, declared time)

The pipeline reduces one consulted handler to an outcome, without touching UI or Jira:

| Answer | Writes | Declared vs confirmed | Outcome | Timer |
|---|---|---|---|---|
| Declined | 0 | – | Run original | Reset if it succeeds |
| Declined | >0 | – | Failed with writes | Reduced by the measured time |
| Handled | any | equal | Succeeded | Reset |
| Handled | any | different | Partial: notice | Reduced by the measured time |
| Cancelled | 0 | – | Nothing | Kept |
| Cancelled | >0 | – | Failed with writes (a cancel after writing is not a clean cancel) | Reduced by the measured time |
| Failed / exception | 0 | – | Fallback to original, visible | Reset if the fallback succeeds |
| Failed / exception | >0 | – | Failed with writes: no fallback | Reduced by the measured time |

The result object returned to `MainWindow` says: the outcome, who handled the load, the number of writes, the time that is in Jira because of it (the confirmed total on success, otherwise the time the host measured) and a reason. It does not carry the notice: the pipeline sends it to the notifier itself (non-modal for the fallback, a message box for the serious cases, nothing for cancels), and only for loads the user started, so the window never has to decide what to say. `MainWindow` resets the timer on success and reduces it by the measured time on a partial load. Tests assert on this table row by row, with a fake handler, a fake original and a fake notifier. The row *Cancelled with writes* is not in the issue; it follows the same logic as *Declined with writes* and closes a gap in the table.

### 5. Validation of `TimeLoaded` is against the confirmed total only

`Handled` is valid when the declared `TimeLoaded` equals the total the host passed in the request (whole minutes, as `TimeElapsedNearestMinute` already is). It is not cross-checked against the measured time: a plugin can post worklogs by other means (through `host.Jira`, for instance), which the counter does not see, so a mismatch between declared and measured would not prove anything. The measured time is used for one thing only: how much to take off the timer after a partial load (decision 13).

*Trade-off:* a plugin can declare the right total and write something else. The host does not claim to catch that.

### 6. The scoped Jira API is a counting decorator, passed in the request

For each `InsteadOf` invocation the pipeline builds a fresh decorator over the shared `PluginJiraApi`. It counts a write only when the underlying call reports success (`AddWorklogAsync` true, `CreateSubtaskAsync` non-null key), and adds up the time of each accepted worklog: that sum is the time the host measures as loaded. It is per invocation, so concurrent loads never share a counter. The time comes from what Jira accepted, not from what the plugin declares, so a plugin cannot make the host subtract more or less than was written.

*Rejected alternative:* an ambient counter (`AsyncLocal`) so that `host.Jira` also counts. It would catch plugins that ignore `request.Jira`, but it is implicit, leaks into background work the plugin starts, and is hard to test. The cost is a documented footgun: writes through `host.Jira` inside a handler are not counted, so a later failure would fall back and may load the time twice. `docs/plugins.md` and the `SplitTime` sample say to use `request.Jira` inside the handler.

### 7. Threading: handlers and observers run on the UI thread

Handlers may open WPF windows. `MainWindow` starts the pipeline from the UI thread and the pipeline awaits handlers without `ConfigureAwait(false)` before them, so they run on the dispatcher. Calling the handler is wrapped in `try/catch` that also covers a throw before the first `await` and a `null` task or result, both treated as `Failed`. The write path itself (`PluginJiraApi`) keeps using `Task.Run` as today, so a handler awaiting it does not block the UI.

### 8. Busy cursor only around host-owned work

`MainWindow.PostWorklogAsync` stops setting the wait cursor for the whole call. The pipeline takes a busy callback and raises it only around the original load, so the cursor is not stuck on `Wait` while a plugin dialog waits for a person.

### 9. One load per issue at a time

`MainWindow` keeps a set of issues with a load in progress and ignores a new post request for the same issue until it finishes. Without it, with no handler timeout and `FireAndForget`, the user could start a second load of the same time while the first one waits in a plugin dialog. A silent ignore is enough: the first load's dialog is already on screen.

### 10. A load started from inside a handler does not consult handlers again

`ITimeLoader.LoadAsync` called while a replacement handler is running (tracked with an `AsyncLocal` flag set by the pipeline for the handler's duration) skips the replacement stage: the original load and the observers still run. This prevents `A → B → A` recursion. A load a plugin starts outside a handler, from a command for example, goes through the full pipeline, with its own handler excluded.

### 11. Notices: non-modal for the fallback, a message box for the serious cases

Severity decides the channel. The **fallback** is informational (the time was loaded) and the issue asks for it to be non-modal: a short line in a new status bar label, themed with the existing brushes, with the detail in a tooltip, kept until the user clicks it or the next load starts. A **partial failure** and a **`TimeLoaded` mismatch** leave time written in Jira and the timer running, and the user has to read them before retrying or risk loading twice, so they use a `MessageBox` with the full text, matching how the app already reports errors (connection error, plugin error). Everything is also logged.

Notices are shown **only for loads whose source is the user**. A load started by a plugin through `ITimeLoader` returns its outcome to that plugin and only logs, so a plugin that imports many entries cannot flood the user with dialogs. The notifier decides the message box owner: the main window when it is visible, otherwise none, so a box raised while the mini view hides the main window still appears. A tray balloon is not needed.

*Alternative:* a status bar label for everything. Rejected for the serious cases: an easy-to-miss line is the wrong channel for something that can double-load time on retry. *Alternative:* a message box for everything. Rejected for the fallback: a broken plugin would interrupt every load with a modal, and the issue asks for non-modal there.

### 12. Contract 1.1 surface

`IJiraApi` gains: an `AddWorklogAsync` overload taking the estimate method and value; `GetSubtasksAsync(parentKey)` returning public descriptions (key, summary) or `null` on failure; `CreateSubtaskAsync(parentKey, summary)` returning the new key or `null`. The host resolves the issue type id itself: it asks `GetSubtaskTypes` for the parent's project and uses the first type, failing if the project offers none. `PluginContract.ContractVersion` and the Abstractions `<Version>` move to 1.1; the existing compatibility check already makes 1.0 plugins load on a 1.1 host and the reverse not.

### 13. The timer after a load: reset on success, reduced on a partial load

The elapsed time is read once, when the dialog closes, and carried in the request, as today. On **success** the timer is reset, as today. If a plugin dialog takes minutes while the timer runs, that reset also discards the time accrued meanwhile, the same behavior as a slow Jira request, only longer; this is accepted.

On a **partial load** (a failure, a decline or a cancel after writes, or a handled load whose time does not match) the timer is not reset: the host reduces it by the time the counting decorator measured. This is what lets a retry load only what is missing, and what makes the fallback on a retry that fails before writing load the remainder instead of the whole time.

- *How:* through the same path as the "Edit Timer" action (`IssueViewModel.SetTimeElapsed`), so the row's notifications stay consistent. The new value is the timer's value **at that moment** minus the measured time, never below zero. That value includes whatever accrued while the handler waited, and a running timer keeps running.
- *Start time:* the recorded start time is kept untouched. The worklog dialog proposes it again for the retry (or "now minus what remains" when that is earlier, as it does today), and the user can edit it there. Advancing it by the loaded time would be more faithful, but it does not apply to a timer whose time was edited by hand, and it adds a second rule for little gain.
- *Comment and estimate:* kept on the row as the user left them, and shown again by the dialog, so the user can adjust them. An estimate of "Reduce by" is applied again to the remainder; the dialog is where the user sees and corrects that.
- *Rounding:* each attempt rounds the timer up to a whole minute, so a chain of partial loads can load up to a minute more than the user tracked. Accepted.
- *Notice:* the message box says how much was loaded and how much remains in the timer, because the timer no longer shows what the user tracked.

*Alternative:* the plugin recognizes the retry as "the same load" (a hash of issue, start and elapsed time kept in its data directory). Rejected after trying it against the running application: the start time of a timer edited by hand is recalculated as "now minus elapsed" on every dialog, and the elapsed time of a running timer keeps growing, so neither is stable between attempts. It also worked per plugin and left the fallback hole open (a retry that failed before writing looked like a failure with no writes and loaded the whole time again).

## Risks / Trade-offs

- [Plugin writes through `host.Jira` instead of `request.Jira`] → Not counted, so a failure falls back and may double-load. Documented; the sample follows the rule. Detectable only by convention.
- [Plugin declares the right `TimeLoaded` but wrote something else] → Not caught. The host validates the declaration, not the writes.
- [Writes made through `host.Jira` are not measured] → The timer is reduced by less than what was written, so a retry may load some time twice. Same cause and same mitigation as the counting risk above: use `request.Jira`.
- [The timer stops showing what the user tracked after a partial load] → The message box says how much was loaded and how much remains, and the log has it.
- [Each retry rounds up to a whole minute] → A chain of partial loads can load up to a minute more than tracked. Accepted; see decision 13.
- [Time accrued during a long plugin dialog is lost on a successful reset] → See decision 13; documented.
- [Fallback notice is missed] → Kept until dismissed or the next load, plus the log. Acceptable: the time was loaded.
- [Two message boxes at once from simultaneous loads on different issues] → Possible but rare; each is about a different issue and its text names it.
- [`IPluginHost` gains members] → Plugins consume it, they do not implement it, so it is additive for them.

## Migration Plan

No data or settings migration. Existing 1.0 plugins keep working. Rolling back is removing the pipeline wiring in `MainWindow` and returning to the direct `IssueJiraService` call; the Abstractions additions are harmless if left.
