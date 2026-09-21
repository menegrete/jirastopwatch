## Why

Jira StopWatch was forked from [jirastopwatch/jirastopwatch](https://github.com/jirastopwatch/jirastopwatch) but is now an independent project at [menegrete/jirastopwatch](https://github.com/menegrete/jirastopwatch). The README still points entirely at the upstream project — its CI badges, download/docs links, and screenshots all reference `jirastopwatch/jirastopwatch` and `jirastopwatch.github.io`, none of which reflect this fork's actual state, workflows, or UI. There is also no documentation of the substantial changes made since the fork (WPF migration, .NET 10, automated releases, dark theme, mini timer, taskbar widget, auto-update), and no docs of the project's own to link to.

## What Changes

- Rewrite `README.md`: fix badges to point at this repo's own workflows, replace the feature/install/docs section with links into the new local `docs/`, and add an explicit "forked from / diverged" section listing the major changes made since independence (WPF + .NET 10 migration, automated releases, dark theme, mini timer view, taskbar widget, auto-update, and smaller UX additions).
- Add a `docs/` directory in-repo (plain Markdown, no separate site/build) with four pages ported and updated from the original `jirastopwatch.github.io` content:
  - `docs/basic-setup.md`
  - `docs/usage.md`
  - `docs/keyboard-shortcuts.md`
  - `docs/advanced-settings.md`
- Replace the three broken/stale screenshot references (hosted on `jirastopwatch.github.io`) with new screenshots taken from the current WPF UI, stored in-repo and referenced by the README and/or docs pages.
- Remove references to upstream-only resources that no longer apply to this fork (e.g. the `jirastopwatch/.github` CONTRIBUTING link), replacing or dropping them as appropriate.
- Add explicit credit/attribution to the original `jirastopwatch/jirastopwatch` project and its authors, beyond what the Apache 2.0 license already requires.

## Capabilities

No spec-level behavior changes — this is documentation only. `skip_specs: true` is set in `.openspec.yaml`.

## Impact

- `README.md` — full rewrite of content and links
- New `docs/` directory (4 new Markdown files)
- New screenshot image assets (replacing external links to `jirastopwatch.github.io`)
- No changes to `source/StopWatch/**`, so this change does not trigger a semantic-release version bump
