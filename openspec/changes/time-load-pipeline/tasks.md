# Tasks

## 1. Contract 1.1: Jira facade additions

- [ ] 1.1 In `StopWatch.Plugin.Abstractions`, add the public subtask description type and a contract estimate-method enum mirroring `EstimateUpdateMethods`; verify the project builds with no host types referenced
- [ ] 1.2 Add to `IJiraApi` the `AddWorklogAsync` overload with estimate method and value, `GetSubtasksAsync` and `CreateSubtaskAsync`; verify the Abstractions project builds
- [ ] 1.3 Implement them in `PluginJiraApi`: map the estimate enum, map `JiraIssueInfo` to the public subtask type, resolve the subtask issue type from `GetSubtaskTypes` of the parent's project (first type, failure when none), and report failure as `null`/`false` without throwing; verify with unit tests using a mocked `IJiraOperations` for success, failure, no subtask types, and an exception
- [ ] 1.4 Bump `PluginContract.ContractVersion` and the Abstractions `<Version>` to 1.1; verify `PluginLoaderTest` covers a 1.0 plugin loading on a 1.1 host and a 1.1 plugin rejected by a 1.0 host
- [ ] 1.5 Document the new facade members and the 1.1 compatibility rule in `docs/plugins.md`; verify the "What `IPluginHost` offers" and contract versioning sections match the code

## 2. Pipeline contract types

- [ ] 2.1 Add the request, source, answer (`Declined`, `Handled(timeLoaded)`, `Cancelled`, `Failed(reason)` through static factories) and `After` event args types to Abstractions; verify they build and that a unit test covers each factory and the source for user and plugin
- [ ] 2.2 Add `IPluginTimeLoad` (register the one `InsteadOf` handler, subscribe to `After`) and `ITimeLoader` to Abstractions and expose them as `IPluginHost.TimeLoad` and `IPluginHost.TimeLoader`; verify the Abstractions project builds

## 3. TimeLoadPipeline (Model)

- [ ] 3.1 Create the registry that holds `(pluginId, handler)` and the `After` subscribers, ordered by id (ordinal, ignoring case) and rejecting a second handler from the same plugin; verify with unit tests for ordering and the duplicate registration
- [ ] 3.2 Create the per-invocation counting decorator over `IJiraApi`, counting only successful `AddWorklogAsync` and `CreateSubtaskAsync`; verify with unit tests for success, rejected write and two concurrent decorators not sharing a count
- [ ] 3.3 Create `TimeLoadPipeline` with the original load, registry, decorator factory, notifier, busy callback and log injected, and implement the outcome table for declined, handled, cancelled and failed with 0 and more than 0 writes; verify one unit test per row of the table in design decision 4, using fakes
- [ ] 3.4 Validate `Handled` against the confirmed total (match resets, mismatch does not reset and notifies loaded vs confirmed); verify with unit tests for equal and different totals
- [ ] 3.5 Wrap each handler call so that a throw before the first await, a `null` task, a `null` result and an exception are all treated as `Failed`; verify with unit tests for each
- [ ] 3.6 Consult handlers in id order, stop at the first that does not decline without passing a failure on to the next, and never consult a plugin for a load whose source is that plugin; verify with unit tests for ordering, stop-at-first, failure not passed on, and own-load exclusion
- [ ] 3.7 Notify `After` observers one by one with the host or plugin id, outcome, reason and write count, isolating a throwing observer, including for cancelled loads; verify with unit tests for host-handled, plugin-handled, a throwing observer and a cancelled load
- [ ] 3.8 Flag a handler's duration with an `AsyncLocal` and skip the replacement stage for a load started inside it; verify with a unit test whose handler calls the loader and sees no second handler consulted
- [ ] 3.9 Raise the busy callback only around the original load; verify with a unit test that it is not raised while a fake handler is waiting
- [ ] 3.10 Verify the whole table, ordering and fallback behavior with no handler registered matches today's `PostWorklogAsync` semantics (success only resets); verify with the existing `IssueJiraServiceTest` untouched and passing

## 4. Plugin host wiring

- [ ] 4.1 Add a per-plugin `PluginTimeLoad` created by the `PluginHost` factory with the plugin id, backed by the shared registry, and expose the `ITimeLoader` that runs the pipeline with source `plugin:<id>`; verify with unit tests that the registered handler carries the right id and that `LoadAsync` uses the plugin source
- [ ] 4.2 Build the pipeline, registry and notifier in `AppComposition` and pass them through `PluginManager.Load`; verify the application builds and starts with no plugins folder and with the HelloWorld sample

## 5. MainWindow integration

- [ ] 5.1 Replace the direct `IssueJiraService` call in `MainWindow.PostWorklogAsync` with the pipeline, reading the elapsed time once and carrying it in the request, and reset the timer from the pipeline's result; verify with no plugin that posting a worklog still resets the timer only on success
- [ ] 5.2 Move the wait cursor to the busy callback so it is not set while a plugin handler is running; verify manually with a handler that waits that the cursor stays normal
- [ ] 5.3 Ignore a new post request for an issue whose load is in progress; verify manually that a second post on the same issue while a plugin dialog is open does nothing
- [ ] 5.4 Add the status bar notice label (themed, kept until clicked or the next load, text logged) and the tray balloon when the main window is not visible; verify manually by forcing a fallback and a partial failure in both light and dark themes with the window visible and hidden
- [ ] 5.5 Document the pipeline, results table, fallback rule, `Source`, raw API versus `ITimeLoader`, the `request.Jira` rule and the idempotency pattern in `docs/plugins.md`; verify every row of the specs' outcome table appears in the document

## 6. SplitTime sample

- [ ] 6.1 Create `source/Samples/SplitTime` (project, `plugin.json`, `SplitTimePlugin`) registering an `InsteadOf` that declines when the issue has no subtasks; verify it builds and the host loads it
- [ ] 6.2 Add the split dialog and the sum-conserving distribution (7 minutes over 3 subtasks gives 3/2/2) using only `request.Jira`; verify with unit tests of the distribution for exact, remainder and single-subtask cases
- [ ] 6.3 Answer cancelled when the user closes the dialog and handled with the loaded total when all writes succeed; verify with an integration test in `PluginSamplesIntegrationTest` for cancel and a successful split
- [ ] 6.4 Answer failed before any write (fallback) and failed after a partial write (no fallback, no reset), and make a retry idempotent with a hash of key, start and elapsed in `DataDirectory`; verify with integration tests for both failures and for a retry after the partial one writing only the missing parts
- [ ] 6.5 Document the sample, the sum conservation and the retry pattern in `docs/plugins.md` and confirm the sample is not part of the release packaging; verify the publish commands do not include it

## 7. Integration check

- [ ] 7.1 Run `dotnet build StopWatch.sln` and `dotnet test StopWatch.sln --settings .runsettings`; verify both pass with `TreatWarningsAsErrors`
- [ ] 7.2 With `SplitTime` installed, verify manually a successful split, a cancel with no effect, a failure before writing with a visible fallback and a partial failure without fallback or timer reset; with no plugins installed, verify posting a worklog behaves exactly as before
