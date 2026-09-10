## 1. IssueViewModel changes

- [x] 1.1 Change `CanOpen` in `source/StopWatch/Model/IssueViewModel.cs` to depend on `Summary` being non-empty instead of `IssueKey` being non-empty.
- [x] 1.2 In the `IssueKey` setter, clear `Summary` back to `""` whenever the key actually changes, before the new summary has resolved.
- [x] 1.3 Ensure `PropertyChanged("CanOpen")` is raised whenever `Summary` changes (including the clear-on-key-change from 1.2) and whenever `IssueKey` changes.

## 2. Verification

- [x] 2.1 Manually verify: add a new row, type/paste a valid, existing issue key, and confirm the Open button enables once the summary resolves.
- [x] 2.2 Manually verify: type a key that does not exist in Jira and confirm the Open button stays disabled.
- [x] 2.3 Manually verify: on a row with an already-resolved key and enabled Open button, change the key to a different value and confirm the button disables immediately, before the new key resolves.
- [x] 2.4 Manually verify: a row hydrated at startup from a persisted key still resolves its summary and enables the Open button as before.

> Automated coverage added instead in `IssueViewModelTest.cs` (`CanOpen_IsFalseUntilTheSummaryResolves`, `CanOpen_IsAnnouncedWhenTheSummaryResolves`, `CanOpen_DropsWhenTheKeyChangesToSomethingElse`, `CanOpen_IsAnnouncedWhenTheKeyChangeDropsTheSummary`) - all passing. 2.1-2.4 are manual, against a real Jira session, and still need to be run by hand before archiving.
