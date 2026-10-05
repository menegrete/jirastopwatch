# Tasks

## 1. Contract 1.1: Jira facade additions

- [x] 1.1 In `StopWatch.Plugin.Abstractions`, add the public subtask description type and a contract estimate-method enum mirroring `EstimateUpdateMethods`; verify the project builds with no host types referenced
- [x] 1.2 Add to `IJiraApi` the `AddWorklogAsync` overload with estimate method and value, `GetSubtasksAsync` and `CreateSubtaskAsync`; verify the Abstractions project builds
- [x] 1.3 Implement them in `PluginJiraApi`: map the estimate enum, map `JiraIssueInfo` to the public subtask type, resolve the subtask issue type from `GetSubtaskTypes` of the parent's project (first type, failure when none), and report failure as `null`/`false` without throwing; verify with unit tests using a mocked `IJiraOperations` for success, failure, no subtask types, and an exception
- [x] 1.4 Bump `PluginContract.ContractVersion` and the Abstractions `<Version>` to 1.1; verify `PluginLoaderTest` covers a 1.0 plugin loading on a 1.1 host and a 1.1 plugin rejected by a 1.0 host
- [x] 1.5 Document the new facade members and the 1.1 compatibility rule in `docs/plugins.md`; verify the "What `IPluginHost` offers" and contract versioning sections match the code

## 2. Pipeline contract types

- [x] 2.1 Add the request, source, answer (`Declined`, `Handled(timeLoaded)`, `Cancelled`, `Failed(reason)` through static factories) and `After` event args types to Abstractions; verify they build and that a unit test covers each factory and the source for user and plugin
- [x] 2.2 Add `IPluginTimeLoad` (register the one `InsteadOf` handler, subscribe to `After`) and `ITimeLoader` to Abstractions and expose them as `IPluginHost.TimeLoad` and `IPluginHost.TimeLoader`; verify the Abstractions project builds

## 3. TimeLoadPipeline (Model)

- [x] 3.1 Create the registry that holds `(pluginId, handler)` and the `After` subscribers, ordered by id (ordinal, ignoring case) and rejecting a second handler from the same plugin; verify with unit tests for ordering and the duplicate registration
- [x] 3.2 Make the per-invocation counting decorator over `IJiraApi` count only successful `AddWorklogAsync` (both overloads) and `CreateSubtaskAsync`, and add up the time of the accepted worklogs; verify with unit tests for success, a rejected write counting and adding nothing, the summed time, and two concurrent decorators not sharing a count or a time
- [x] 3.3 Make `TimeLoadPipeline` return the time the host measured as loaded alongside the outcome, and implement the outcome table for declined, handled, cancelled and failed with 0 and more than 0 writes; verify one unit test per row of the table in design decision 4, including the measured time on each partial row (and that it is the measured time, not the declared one), using fakes
- [x] 3.4 Validate `Handled` against the confirmed total (match resets; mismatch does not reset, carries the measured time and notifies loaded vs remaining); verify with unit tests for equal and different totals, and one where the declared time differs from the measured one
- [x] 3.5 Wrap each handler call so that a throw before the first await, a `null` task, a `null` result and an exception are all treated as `Failed`; verify with unit tests for each
- [x] 3.6 Consult handlers in id order, stop at the first that does not decline without passing a failure on to the next, and never consult a plugin for a load whose source is that plugin; verify with unit tests for ordering, stop-at-first, failure not passed on, and own-load exclusion
- [x] 3.7 Notify `After` observers one by one with the host or plugin id, outcome, reason, write count and the measured time loaded, isolating a throwing observer, including for cancelled loads; verify with unit tests for host-handled, plugin-handled, a partial load reporting the measured time, a throwing observer and a cancelled load
- [x] 3.8 Flag a handler's duration with an `AsyncLocal` and skip the replacement stage for a load started inside it; verify with a unit test whose handler calls the loader and sees no second handler consulted
- [x] 3.9 Raise the busy callback only around the original load; verify with a unit test that it is not raised while a fake handler is waiting
- [x] 3.10 Verify the whole table, ordering and fallback behavior with no handler registered matches today's `PostWorklogAsync` semantics (success only resets); verify with the existing `IssueJiraServiceTest` untouched and passing
- [x] 3.11 Add, in `Model/`, the rule for what the timer holds after a load: reset on success, reduced by the measured time on a partial load, never below zero, keeping the running or paused state and the recorded start time, and reducing a running timer from its value at that moment; verify with unit tests for reduce, floor at zero, running timer, kept start time, success reset, and a retry after a partial load that falls back and loads only the remainder

## 4. Plugin host wiring

- [x] 4.1 Add a per-plugin `PluginTimeLoad` created by the `PluginHost` factory with the plugin id, backed by the shared registry, and expose the `ITimeLoader` that runs the pipeline with source `plugin:<id>`; verify with unit tests that the registered handler carries the right id and that `LoadAsync` uses the plugin source
- [x] 4.2 Build the pipeline, registry and notifier in `AppComposition` and pass them through `PluginManager.Load`; verify the application builds and starts with no plugins folder and with the HelloWorld sample

## 5. MainWindow integration

- [x] 5.1 Replace the direct `IssueJiraService` call in `MainWindow.PostWorklogAsync` with the pipeline, reading the elapsed time once and carrying it in the request, and reset the timer from the pipeline's result; verify with no plugin that posting a worklog still resets the timer only on success
- [x] 5.2 Move the wait cursor to the busy callback so it is not set while a plugin handler is running; verify manually with a handler that waits that the cursor stays normal
- [x] 5.3 Ignore a new post request for an issue whose load is in progress; the manual check does not apply: a plugin dialog is modal over the main window, so there is no way through the UI to ask for a second post while a load waits, and the guard (a set of issues with a load in progress, checked at the start of `PostWorklog`) is verified by inspection only
- [x] 5.4 Add the non-modal status bar label for the fallback (short line, detail in the tooltip, themed, kept until clicked or the next load) and verify manually by forcing a fallback in both light and dark themes
- [x] 5.5 Show a message box for a partial failure and a `TimeLoaded` mismatch, owned by the main window only when it is visible, saying how much was loaded and how much remains in the timer; verify manually by forcing both, with the window visible and with the mini view hiding it
- [x] 5.8 In `MainWindow.PostWorklogAsync`, apply the timer rule from 3.11 to the pipeline's result (through `IssueViewModel.SetTimeElapsed`, as "Edit Timer" does, and keeping the row's comment and estimate on a partial load); verify manually that after a partial failure the row shows only the remaining time, that a retry loads just that and that the row resets after it succeeds
- [x] 5.6 Show notices only for loads whose source is the user and log the rest; verify with a unit test that a plugin-sourced failure produces no notice request, and that a user-sourced one does for each kind
- [x] 5.7 Document in `docs/plugins.md` the pipeline, the results table with the timer column, the fallback rule, `Source`, raw API versus `ITimeLoader`, the `request.Jira` rule (writes through `host.Jira` are neither counted nor subtracted), and how a retry works now: the timer is reduced by the measured time, the start time, comment and estimate are kept, and "Reduce by" applies again; remove the idempotency pattern and the `Cancelled` trick; verify every row of the specs' outcome table appears in the document and that no mention of the hash pattern is left

## 6. SplitTime sample

- [x] 6.1 Create `source/Samples/SplitTime` (project, `plugin.json`, `SplitTimePlugin`) registering an `InsteadOf` that declines when the issue has no subtasks; verify it builds and the host loads it
- [x] 6.2 Add the split dialog and the sum-conserving distribution (7 minutes over 3 subtasks gives 3/2/2) using only `request.Jira`; verify with unit tests of the distribution for exact, remainder and single-subtask cases
- [x] 6.3 Answer cancelled when the user closes the dialog and handled with the loaded total when all writes succeed; verify with an integration test in `PluginSamplesIntegrationTest` for cancel and a successful split
- [x] 6.4 Answer failed before any write (fallback) and failed after a partial write (no fallback, the host reduces the timer), with no memory of earlier attempts: remove `SplitLedger`, the resume path and the `Cancelled` answer after earlier progress from `SplitTime`; verify with integration tests for both failures, that the failure after a partial write reports the measured time, and that a retry over the reduced total splits only what remains
- [x] 6.5 Document the sample and the sum conservation in `docs/plugins.md` without the retry pattern, and confirm the sample is not part of the release packaging; verify the publish commands do not include it

## 7. Integration check

- [x] 7.1 Run `dotnet build StopWatch.sln` and `dotnet test StopWatch.sln --settings .runsettings`; verify both pass with `TreatWarningsAsErrors`
- [x] 7.2 With `SplitTime` installed, verify manually a successful split, a cancel with no effect, a failure before writing with a visible fallback, a partial failure without fallback whose timer is reduced to the remainder, a retry that completes it, and a retry that fails before writing and falls back loading only the remainder; with no plugins installed, verify posting a worklog behaves exactly as before
