# Basic usage

## Adding an issue

Click the **+** button (or press `Ctrl+N`) to add a new row. Type or paste a Jira issue key (e.g. `ABC-123`) into the key field — pasting a full issue URL also works, the key is extracted automatically. Press Enter or move focus away from the field to resolve the issue's summary from Jira.

The number of rows you can have at once is capped by the **Max. display issues** setting (see [Advanced settings](advanced-settings.md)).

To remove a row, click its remove button or press `Ctrl+Delete`. You can't remove the last remaining row.

## Tracking time

Click a row's play/pause button (or press `Ctrl+P` with the row selected) to start or stop its timer.

By default, only one timer can run at a time — starting a new one automatically pauses whichever was running. If you turn on **Allow running multiple timers simultaneously** in Settings, several timers can run at once, up to a configurable limit.

To edit the elapsed time by hand, double-click the time on a row (or press `Ctrl+E`) and enter a duration using Jira's time notation (e.g. `2h 15m` or `2.25h`). To discard the accumulated time and any comment, click the reset button (or press `Ctrl+R`).

## Logging work to Jira

Click a row's post button (or press `Ctrl+L`) to submit the tracked time as a Jira worklog. This opens a dialog where you can:

- Add an optional comment (press `Ctrl+Enter` to submit directly from the comment field)
- Adjust the worklog's start date/time
- Choose how the issue's remaining estimate should be updated: adjust automatically, leave unchanged, set to a specific value, or reduce by a specific amount

Choose **Submit** to post the worklog and reset the timer, **Save for later** to keep your comment and estimate choice on the row without posting yet, or **Cancel** to discard.

Jira doesn't accept worklogs under one minute, so the post button stays disabled until at least a minute has accumulated.

## Other row actions

- **Open in browser** (`Ctrl+O`) — opens the issue in your default browser (available once the summary has resolved)
- **Copy issue key** — copies the key to the clipboard; subtasks also get a copy-parent-key button
- **Select previous/next row** — `Ctrl+Up` / `Ctrl+Down`
- **Focus the key field** — `Ctrl+I`

See the full list in [Keyboard shortcuts](keyboard-shortcuts.md).

## Mini timer view

Click the mini-view button, or minimize the main window (if configured to do so — see [Advanced settings](advanced-settings.md)), to shrink down to a small always-on-top floating window showing your running timer(s), with its own pause/resume, copy-key, and open-in-browser controls. Double-click its background, or use its restore button, to bring back the full window.
