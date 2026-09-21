## 1. Model

- [x] 1.1 Add `IsFirst`/`IsLast` boolean properties to `IssueViewModel`, same pattern as `IsCurrent` (backing field, `Set(ref field, value, "Name")`)
- [x] 1.2 In `IssueListViewModel.Issues_CollectionChanged`, recompute `IsFirst`/`IsLast` across `Issues` (first/last row true, rest false), alongside the existing `CanAdd`/`CanRemove` raises
- [x] 1.3 Add `IssueListViewModel.MoveUp(IssueViewModel)` / `MoveDown(IssueViewModel)` helpers that compute the target index and call the existing `Move`, so callers don't recompute `IndexOf(issue) ± 1` themselves

## 2. Commands

- [x] 2.1 Add `MoveUp` (`Ctrl+Shift+Up`) and `MoveDown` (`Ctrl+Shift+Down`) to `StopWatchCommands.cs`, same `Make(...)` helper as the existing twelve
- [x] 2.2 Bind both commands in `MainWindow.xaml.cs` to call `issues.MoveUp(issues.Current)` / `issues.MoveDown(issues.Current)` on the selected row, same registration style as `SelectPrevious`/`SelectNext` (also extended `MainWindow_PreviewKeyDown` - `Ctrl+Shift+Up/Down` collides with the TextBox's built-in "extend selection by paragraph" editing command the same way plain `Ctrl+Up/Down` already did)

## 3. Row buttons — compact template

- [x] 3.1 Add move-up/move-down `Button`s to `IssueRowCompact`, overlaid on `Grid.Column="3"` (summary column's right edge - it has slack to spare, unlike the Auto-sized start/stop column, so no reflow on hover), sized 16x14, `Visibility="Collapsed"` by default (superseded the `Grid.Column="4"`/`HorizontalAlignment="Left"` placement from design.md D2 - that column is `Auto`-sized to the play button exactly, so overlaying there would grow the column, and the row, on every hover)
- [x] 3.2 Wire their `Click` handlers to call `issues.MoveUp(issue)` / `issues.MoveDown(issue)` on that row's own `IssueViewModel` (the row's `DataContext`)
- [x] 3.3 Add both buttons to the existing hover `Trigger` (`SourceName="row" Property="IsMouseOver"`) that already reveals `btnCopyKey`/`btnCopyParentKey`
- [x] 3.4 Add `DataTrigger`s for `IsFirst`/`IsLast` (`Value="True"` → `Collapsed` on the corresponding button), placed after the hover trigger in `DataTemplate.Triggers` so they win over it, same ordering already used for `CanOpen`/`HasParent`

## 4. Row buttons — spacious template

- [x] 4.1 Repeat 3.1-3.4 in `IssueRowSpacious`

## 5. Tests

- [x] 5.1 `IssueListViewModelTest`: `Move` reorders `Issues` and keeps the moved row selected (covers the existing but untested method)
- [x] 5.2 `IssueListViewModelTest`: `Move` is a no-op past either end of the list
- [x] 5.3 `IssueListViewModelTest`: `IsFirst`/`IsLast` are correct after `Hydrate`, after `Add`, after `Remove`, and after `Move` (including the row that stops being first/last)
- [x] 5.4 `IssueListViewModelTest`: `MoveUp`/`MoveDown` move the given row regardless of which row is selected, and leave list order otherwise unchanged (plus dedicated boundary tests for `MoveUp` on the first row and `MoveDown` on the last)
- [x] 5.5 `IssueViewModelTest`: `IsFirst`/`IsLast` raise `PropertyChanged` when set

## 6. Manual verification

- [x] 6.1 In both densities, confirm the move buttons appear only on hover, sit left of start/stop, and are absent on the first/last row respectively
- [x] 6.2 Confirm `Ctrl+Shift+Up`/`Ctrl+Shift+Down` move the selected row and it stays selected
- [x] 6.3 Confirm a running timer keeps running across a move (button or shortcut)
- [x] 6.4 Restart the app after reordering and confirm the new order persisted
