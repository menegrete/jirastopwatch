## Context

See proposal.md - Why. Current CI (`build.yml`) only builds and tests; nothing tags, versions, or publishes. Commit history on `main` is already disciplined Conventional Commits (`feat:`, `fix:`, `chore:`), and PRs land as real merge commits, so per-commit messages survive onto `main` intact - this is what makes commit-driven versioning viable without changing team workflow. `AssemblyInfo.cs` carries the version by hand (`GenerateAssemblyInfo` is deliberately off, per its own comment) and `CHANGELOG.md` is keyed to that same version by convention (per `CLAUDE.md`), not enforced.

## Goals / Non-Goals

**Goals:**
- Zero manual steps between "PR merged to main" and "release exists" for releasable changes.
- Reuse the existing conventional-commit discipline instead of introducing a new commit format or PR-title convention.
- Keep `AssemblyInfo.cs` and `CHANGELOG.md` as the source of truth for version/history (per `CLAUDE.md`), just machine-written instead of hand-written.

**Non-Goals:**
- Not changing how PRs are reviewed/merged, or requiring squash merges.
- Not introducing a release-candidate/pre-release channel - every release is a "final" release on `main`.
- Not covering distribution beyond GitHub Releases (e.g. auto-publishing to jirastopwatch.com is out of scope here).

## Decisions

**Tool: `semantic-release`.** Chosen over a hand-rolled script because the repo's commit history already matches its exact input format (Conventional Commits); it natively handles version computation, tagging, changelog generation, and GitHub Release creation, and its `exec`/`git` plugins cover the two .NET-specific gaps (writing `AssemblyInfo.cs`, running `dotnet publish`) without reimplementing the rest. Alternative considered: a custom script parsing `git log` since the last tag - rejected as reinventing a well-tested tool for no repo-specific reason.

**Placement: extend `build.yml` with a `release` job, rather than a standalone workflow.** GitHub Actions' `paths:` trigger filter (needed to scope releases to `source/StopWatch/**`) only applies to `push`/`pull_request` triggers, not `workflow_run` - so a separate release workflow gated on `build.yml`'s success would need to either re-check the path filter itself or poll `build.yml`'s run status via the API, both more complex than adding one job to the workflow that already has the push trigger, the path context, and the test results. Concretely: add a `release` job to `build.yml`, `needs: [build]` (the test job, Release configuration), `if:` restricted to `github.event_name == 'push' && github.ref == 'refs/heads/main'`, with a `dorny/paths-filter`-style step to check whether the push touched `source/StopWatch/**` before running `semantic-release`. This also means `continue-on-error: true` on the existing `build` job's test step needs revisiting - it currently lets a red test run finish "successfully" from the workflow's point of view, which would defeat the release gate; the release job's `needs` condition must key off the test outcome, not merely job completion.

**Changelog format: keep Keep a Changelog structure (`Added`/`Changed`/`Fixed`/`Removed`), not semantic-release's default Angular-style output.** This requires configuring `@semantic-release/release-notes-generator` with a custom preset/type-to-section mapping (`feat` → Added, `fix` → Fixed, etc.) instead of accepting the default "Features"/"Bug Fixes" headings. Chosen to avoid a jarring format break in `CHANGELOG.md` history and because the existing entries already use these categories. Alternative (accept the Angular default) would need less config but changes the changelog's look with no benefit to compensate.

**Auth: default `GITHUB_TOKEN` with "Read and write permissions", not a separate PAT**, unless branch protection on `main` turns out to require it. This is the smaller change to repo configuration; a PAT is the fallback documented in proposal.md's Impact section if the default token can't push past branch protection.

**Bundled removals ride in this same change**, since they're both trivial, unrelated-to-behavior deletions discovered while auditing CI for this work: `dispatch.yml` (dead generic broadcaster, no consumer found) and `codeql.yml`'s `dependency-scan` job (duplicates the native Dependabot `github-actions` update already in `dependabot.yml`). Neither has its own capability/spec since neither describes app or release behavior - they're pure repo housekeeping, covered by proposal.md's Impact section and tasks.md.

## Risks / Trade-offs

- **[Risk]** The release job pushes a commit + tag directly to `main` outside normal PR review → **Mitigation**: this is an accepted, minimal, machine-generated commit (version files + changelog only); scope the bot's write access narrowly and don't let it touch anything else.
- **[Risk]** `continue-on-error: true` on the existing `build` job currently masks red tests from failing the workflow → **Mitigation**: the release job's gating must explicitly check the test step's outcome (e.g. `needs.build.result` combined with the specific test step's `outcome`, or removing `continue-on-error` for the Release-configuration leg), not just `needs: [build]` succeeding.
- **[Risk]** A repo-wide `package.json` at the root is new territory for a .NET-only repo (dependency scanners, editors, etc. may assume a Node project) → **Mitigation**: keep it minimal (`private: true`, only `semantic-release` + its plugins as devDependencies), scoped clearly by name/description.
- **[Risk]** If branch protection on `main` blocks direct pushes even from `GITHUB_TOKEN`, releases silently fail until permissions are fixed → **Mitigation**: this is called out explicitly in proposal.md's Impact section as a prerequisite to verify before this change is applied.

## Migration Plan

1. Land the workflow/config changes (this change) without deleting the manual-process documentation until the first automated release succeeds end-to-end.
2. Verify repo settings (Actions read/write permissions, branch protection bypass) before merging - the release job will silently no-op or fail otherwise.
3. First push to `main` touching `source/StopWatch/**` after merge exercises the full pipeline; confirm the tag, release, changelog entry, and both artifacts before relying on it.
4. Once confirmed, update `CLAUDE.md`'s versioning section to describe the automated flow (already scoped in proposal.md's Impact).
5. Rollback: disable/remove the `release` job (or revert this change) - `build.yml`'s existing build/test behavior is unaffected either way, since it was additive.
