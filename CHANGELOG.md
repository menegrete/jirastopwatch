# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project's version numbers are the ones in
`source/StopWatch/Properties/AssemblyInfo.cs`
(`AssemblyVersion`/`AssemblyFileVersion`/`AssemblyInformationalVersion`).

Entries for versions released before this file existed are not
reconstructed here — see `git log` and the archived proposals under
`openspec/changes/archive/` for that history.

## [Unreleased]

### Added

- The mini timer view now lists every running timer as its own row (key, summary, time and its own pause/resume control), instead of showing only one timer with a "+1" indicator when others are running. With a single timer running it looks exactly as before.
- The mini view now grows away from whichever screen edge it is anchored to when it needs more rows, instead of always growing downward.
- Double-clicking the mini view's background now also returns to the full window, in addition to the existing button.
- New setting, "Max. simultaneous timers", enabled when "Allow running multiple timers simultaneously" is on (default 3). Once that many timers are running, starting another one has no effect until one of them is paused — from either the main window or the mini view.

### Changed

- In the mini view, the control to return to the full window is now a single button that floats over the whole pill instead of one column per row, so it stays in the same place no matter how many timers are shown.

### Fixed

- Returning from the mini view to the full window no longer leaves it off-screen when the monitor it was on got disconnected while the mini view was up — it now falls back to a visible position on the main screen, the same protection the mini view's own position already had.
