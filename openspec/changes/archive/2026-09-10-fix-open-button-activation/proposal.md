## Why

The per-row "open issue in browser" button is meant to be usable only once its issue key resolves to a real, existing Jira issue. Today it enables as soon as the key field is non-empty, and even that check is broken: it never re-evaluates after the key changes, so on freshly added rows the button silently stays in whatever enabled state it had when the row was first bound, no matter what the user types or pastes into it.

## What Changes

- Redefine the Open button's enabled condition to depend on the row having a resolved summary from Jira, not on the raw key text. A non-empty summary is the existing signal that the key was valid and the issue was found.
- Clear the row's summary immediately when its issue key changes, so the button disables right away instead of staying enabled against a stale summary from the previous key while the new one is still resolving.
- Fix the missing change notification so the button's enabled state actually updates when the underlying condition changes, instead of being frozen at whatever value it had when the row was first bound.

## Capabilities

### New Capabilities
- `issue-open-action`: Governs when a row's "open in browser" action is available - specifically, that it requires a Jira-confirmed issue (a resolved summary) rather than merely a non-empty key, and that this availability updates live as the key and summary change.

### Modified Capabilities
(none - no existing capability spec covers this behavior)

## Impact

- `source/StopWatch/Model/IssueViewModel.cs`: `CanOpen` getter, `IssueKey` and `Summary` setters (notification wiring).
- `source/StopWatch/UI/MainWindow.xaml.cs`: `UpdateSummaryAsync` (where the old summary would need clearing before/while a new one resolves).
- No API or persistence format changes; purely in-memory UI state.
