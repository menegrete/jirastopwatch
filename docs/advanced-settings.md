# Advanced settings

All of these live in the Settings window (the button next to the connection status in the main window).

## Display options

- **Always keep window on top** — keeps the main window above other applications.
- **Include project name in issue summary** — shows the project name alongside the issue summary.
- **Minimize to** — where the main window goes when minimized: **Mini View** (a small floating always-on-top window with your running timers), **Tray** (the system tray), or **Taskbar Widget** (a compact widget docked to a chosen monitor). Choosing Taskbar Widget disables **Allow running multiple timers simultaneously**, since the widget can only display one active timer at a time.
  - **Taskbar widget monitor** — when Taskbar Widget is selected, choose which connected monitor it appears on.
- **Theme** — **Dark** or **Light**. There's no "follow system theme" option.
- **Issue list density** — **Compact** or **Spacious** row spacing.
- **Max. display issues** — how many issue rows you can have at once (1–40).

## General options

- **Allow running multiple timers simultaneously** — when off (the default), starting a timer pauses any other running timer. When on, several timers can run at once.
  - **Max. simultaneous timers** — caps how many can run concurrently when multiple timers are allowed (2–20).
- **Save timer states on program exit** — what happens to running/accumulated time when you close the app: reset everything, save the tracked time and pause, or save the tracked time and keep the active timer running.
- **Pause timer on session lock** — whether locking your Windows session (Win+L) pauses the active timer, and whether unlocking resumes it.
- **How to post the worklog comment** — whether your worklog comment is posted only as part of the worklog, only as a separate issue comment, or both.
- **Possible state changes when pressing play** — a list of Jira workflow transition names (one per line). When you start a timer, Jira StopWatch looks for a matching available transition on that issue and triggers it automatically — handy for moving an issue to "In Progress" the moment you start working on it.

## Updates

- **Automatically check for and install updates** — on by default. Jira StopWatch checks for a newer release at startup, downloads it in the background, and applies it the next time you close the app normally (never mid-session or on a forced close).

## Diagnostics

- **Enable debug logging** — turns on verbose logging for troubleshooting.
- **Open log folder** — opens the folder containing the current log file in Explorer.
