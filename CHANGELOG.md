# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project's version numbers are the ones in
`source/StopWatch/Properties/AssemblyInfo.cs`
(`AssemblyVersion`/`AssemblyFileVersion`/`AssemblyInformationalVersion`).

Entries for versions released before this file existed are not
reconstructed here — see `git log` and the archived proposals under
`openspec/changes/archive/` for that history.

## [3.7.0](https://github.com/menegrete/jirastopwatch/compare/v3.6.0...v3.7.0) (2026-09-23)

### Added

* link to release notes from the update-ready notice ([f792d61](https://github.com/menegrete/jirastopwatch/commit/f792d61405a8bafab7daaf62864001ec13b028ab))

## [3.6.0](https://github.com/menegrete/jirastopwatch/compare/v3.5.0...v3.6.0) (2026-09-22)

### Added

* allow choosing how elapsed time is displayed in the Main Window ([cedb919](https://github.com/menegrete/jirastopwatch/commit/cedb919d420b4b7322442e8297dc8a25a7ab6ac5))

## [3.5.0](https://github.com/menegrete/jirastopwatch/compare/v3.4.0...v3.5.0) (2026-09-22)

### Added

* remember main window position and maximized state between runs ([b52fb78](https://github.com/menegrete/jirastopwatch/commit/b52fb78a1ccab89f40b0679ad90b9752b98758e0)), closes [#29](https://github.com/menegrete/jirastopwatch/issues/29)

## [3.4.0](https://github.com/menegrete/jirastopwatch/compare/v3.3.0...v3.4.0) (2026-09-21)

### Added

* recheck for releases hourly while running; hide mini view button under Taskbar Widget ([abeb713](https://github.com/menegrete/jirastopwatch/commit/abeb7136af84db933265a10def61da4911f96514))

## [3.3.0](https://github.com/menegrete/jirastopwatch/compare/v3.2.0...v3.3.0) (2026-09-21)

### Added

* move issues up/down in the list ([fafbf1c](https://github.com/menegrete/jirastopwatch/commit/fafbf1ce1fd1c0e1c975e6df84a335ab324916bb)), closes [#16](https://github.com/menegrete/jirastopwatch/issues/16) [#26](https://github.com/menegrete/jirastopwatch/issues/26)

### Fixed

* refresh the active issue's time when returning from the mini view ([01517d9](https://github.com/menegrete/jirastopwatch/commit/01517d9a1c2cae963408bbdd875d4f5c7e61d573)), closes [#26](https://github.com/menegrete/jirastopwatch/issues/26)

## [3.2.0](https://github.com/menegrete/jirastopwatch/compare/v3.1.2...v3.2.0) (2026-09-19)

### Added

* add taskbar widget as a minimize destination ([3dc4fe0](https://github.com/menegrete/jirastopwatch/commit/3dc4fe041a5f840d698c2edc4a2ce973b41250a7)), closes [#14](https://github.com/menegrete/jirastopwatch/issues/14)

## [3.1.2](https://github.com/menegrete/jirastopwatch/compare/v3.1.1...v3.1.2) (2026-09-18)

### Fixed

* trim the trailing separator that broke the apply helper's quoting ([#22](https://github.com/menegrete/jirastopwatch/issues/22)) ([de9f138](https://github.com/menegrete/jirastopwatch/commit/de9f1381b2dbe4738079622093ef5c5f49cdc80d))

## [3.1.1](https://github.com/menegrete/jirastopwatch/compare/v3.1.0...v3.1.1) (2026-09-18)

### Fixed

* retry the auto-update swap when the new file is briefly locked ([#21](https://github.com/menegrete/jirastopwatch/issues/21)) ([ed77e39](https://github.com/menegrete/jirastopwatch/commit/ed77e393e2c8dc56c258453227712df674ab5e99))

## [3.1.0](https://github.com/menegrete/jirastopwatch/compare/v3.0.0...v3.1.0) (2026-09-18)

### Added

* check for, download and install app updates automatically ([#20](https://github.com/menegrete/jirastopwatch/issues/20)) ([df7c9a6](https://github.com/menegrete/jirastopwatch/commit/df7c9a6dda16fd395cc94e235187c8055976176b))

## [3.0.0](https://github.com/menegrete/jirastopwatch/compare/v2.5.0...v3.0.0) (2026-09-18)

### ⚠ BREAKING CHANGES

* minimizing the main window no longer falls back to a
plain taskbar minimize. It now goes to the mini view by default, or to
the tray if that combo option is selected in Settings.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>

### Added

* minimize main window to the mini view by default ([211f252](https://github.com/menegrete/jirastopwatch/commit/211f252a54aeaa4fba4bc9fdc8b98cd2424a9f4a)), closes [#15](https://github.com/menegrete/jirastopwatch/issues/15)

## [2.5.0](https://github.com/menegrete/jirastopwatch/compare/v2.4.0...v2.5.0) (2026-09-18)

### Added

* copy issue key and open-in-browser buttons in the mini view ([18532f6](https://github.com/menegrete/jirastopwatch/commit/18532f6dbb282b1206e193b3e1b2509d378910b6))

### Fixed

* hide the main window's copy-key icon until the issue resolves ([7876a61](https://github.com/menegrete/jirastopwatch/commit/7876a61de9128a688353ca1135be6f6de107205e))
* refresh mini view row's Summary so the open button reacts once resolved ([9b2c18d](https://github.com/menegrete/jirastopwatch/commit/9b2c18dcaf57dba5f0c5243e138229b09dba4932))
* repair the release-duplicated CHANGELOG.md and pin its line endings ([5aa2f84](https://github.com/menegrete/jirastopwatch/commit/5aa2f849fb584194060c41688839d66c1e659ffb))

## [Unreleased]

### Added

- The mini timer view now lists every running timer as its own row (key, summary, time and its own pause/resume control), instead of showing only one timer with a "+1" indicator when others are running. With a single timer running it looks exactly as before.
- The mini view now grows away from whichever screen edge it is anchored to when it needs more rows, instead of always growing downward.
- Double-clicking the mini view's background now also returns to the full window, in addition to the existing button.
- New setting, "Max. simultaneous timers", enabled when "Allow running multiple timers simultaneously" is on (default 3). Once that many timers are running, starting another one has no effect until one of them is paused — from either the main window or the mini view.
- Hovering over a row's key in the mini view now reveals a button to copy that key and a button to open the issue in the browser, matching what the main window already offers for its own rows.

### Changed

- In the mini view, the control to return to the full window is now a single button that floats over the whole pill instead of one column per row, so it stays in the same place no matter how many timers are shown.

### Fixed

- Returning from the mini view to the full window no longer leaves it off-screen when the monitor it was on got disconnected while the mini view was up — it now falls back to a visible position on the main screen, the same protection the mini view's own position already had.
- In the main window, hovering a row no longer shows the copy-key icon when that row's issue hasn't resolved against Jira yet (an empty row, or a key just typed/pasted) — it now follows the same "resolved issue required" rule as opening in the browser.

## [2.4.0](https://github.com/menegrete/jirastopwatch/compare/v2.3.1...v2.4.0) (2026-09-17)

### Added

* automate versioning, changelog and GitHub Releases via semantic-release ([d6dde74](https://github.com/menegrete/jirastopwatch/commit/d6dde74d4effaf432bd905b8478d565b6d1e6570))

### Fixed

* add package-lock.json so the release job's npm ci succeeds ([df100ca](https://github.com/menegrete/jirastopwatch/commit/df100ca1ad5bd119b5b1ac3e31739f0c4500ecd3)), closes [#12](https://github.com/menegrete/jirastopwatch/issues/12)
