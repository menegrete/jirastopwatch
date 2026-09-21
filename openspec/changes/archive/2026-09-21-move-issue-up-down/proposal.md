## Why

Issues arrive in the list in whatever order the user added them, and there is
no way to change that afterward short of removing and re-adding rows. GitHub
issue #16 asks for a way to move a row up or down. The reorder mechanics
already exist in the model (`IssueListViewModel.Move`) but are not exposed by
either UI framework the app uses.

## What Changes

- Add `Ctrl+Shift+Up` / `Ctrl+Shift+Down` keyboard shortcuts that move the
  selected row up or down one position, following the existing
  `StopWatchCommands` pattern (`Ctrl+Up`/`Ctrl+Down` already select the
  previous/next row).
- Add floating up/down buttons on each row, visible only on hover, overlaid
  to the left of the start/stop button — the same hover-reveal technique the
  copy-key buttons already use, so no new grid column is needed.
- Hide (not just disable) the up button on the first row and the down button
  on the last row. This requires each row to know its own position in the
  list, via new `IsFirst`/`IsLast` properties on `IssueViewModel`, recomputed
  by `IssueListViewModel` whenever the row collection changes (add, remove,
  move).
- Wire both the shortcuts and the buttons to the existing
  `IssueListViewModel.Move` method.
- Both the compact and spacious row templates need the button changes, since
  they are separate `DataTemplate`s today.

Drag-and-drop reordering was considered and explicitly deferred: the list is
a plain `ItemsControl` with no existing drag infrastructure, every row is
already full of interactive controls (so a drag would need its own handle,
competing with the same hover-reveal space as the up/down buttons), and it
would not replace the keyboard shortcut anyway. It can be revisited as a
separate change later if the button/shortcut combination turns out not to be
enough.

## Capabilities

### New Capabilities
- `issue-reordering`: lets the user move a row up or down in the issue list,
  via keyboard shortcut and via hover-revealed buttons per row, and defines
  how that order persists.

### Modified Capabilities

(none — `issue-list` governs row presentation, not row order; introducing
order as a new capability keeps that spec unchanged)

## Impact

- `source/StopWatch/Model/IssueListViewModel.cs`: recompute `IsFirst`/`IsLast`
  on collection changes; no change to `Move` itself.
- `source/StopWatch/Model/IssueViewModel.cs`: new `IsFirst`/`IsLast`
  properties.
- `source/StopWatch/UI/StopWatchCommands.cs`: two new commands.
- `source/StopWatch/UI/MainWindow.xaml` / `MainWindow.xaml.cs`: hover-reveal
  buttons in both row templates, command bindings.
- `source/StopWatchTest/`: new/updated tests for `IssueListViewModel` (move
  behavior, `IsFirst`/`IsLast` recomputation) and `IssueViewModel`.
- No change to `Settings`/`PersistedIssue` — the list already persists in
  `Issues` order via `IssueListViewModel.Persist()`, so a moved row is
  captured under the new order automatically at the next save.
