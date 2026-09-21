## 1. New screenshots

- [x] 1.1 Run the app locally (light theme) and capture a screenshot of the main window with a few issues listed
- [x] 1.2 Capture a screenshot of the dark theme
- [x] 1.3 Capture a screenshot of the mini timer view
- [x] 1.4 Save the captures under `docs/img/` (e.g. `main-window.png`, `dark-theme.png`, `mini-timer.png`), sized/cropped for README display

## 2. docs/ content

- [x] 2.1 Create `docs/basic-setup.md`: Jira connection, credentials/API token, first-run configuration, ported and updated from the original `jirastopwatch.github.io` "basic setup" content
- [x] 2.2 Create `docs/usage.md`: adding issues, starting/pausing/switching timers, the single-timer-unless-multiple-allowed rule, logging work to Jira, ported and updated from the original "basic usage" content
- [x] 2.3 Create `docs/keyboard-shortcuts.md`: current keyboard shortcuts, ported and verified against the current app (not just copied from upstream)
- [x] 2.4 Create `docs/advanced-settings.md`: settings covering theme, mini timer view, taskbar widget, auto-update, multi-timer, max issues limit, and other current configuration options
- [x] 2.5 Cross-link the four docs pages to each other where relevant

## 3. README rewrite

- [x] 3.1 Fix CI badges (CodeQL, Build) to point at `menegrete/jirastopwatch` workflows
- [x] 3.2 Replace the "Features, download and installation" section: point to the local `docs/` pages instead of `jirastopwatch.github.io`
- [x] 3.3 Add a "Fork history" (or similarly named) section stating this project forked from `jirastopwatch/jirastopwatch` and is now independent, listing the major changes made since independence (WPF + .NET 10 migration, automated releases, dark theme, mini timer view, taskbar widget, auto-update, plus smaller UX additions)
- [x] 3.4 Add explicit credit/attribution to the original project and its authors
- [x] 3.5 Replace the three screenshot links with the new local images from `docs/img/`
- [x] 3.6 Remove/replace links to upstream-only resources that don't apply to this fork (e.g. `jirastopwatch/.github` CONTRIBUTING link, `jirastopwatch.github.io` homepage link)
- [x] 3.7 Update the "Feedback" section to point at this repo's own Issues page

## 4. Validation

- [x] 4.1 Click through every link in the rewritten README and confirm none 404
- [x] 4.2 Confirm no remaining references to `jirastopwatch/jirastopwatch` or `jirastopwatch.github.io` other than the explicit fork-history/credit mentions
