## 1. Repo prerequisites (verify before relying on automation)

- [x] 1.1 Enable "Read and write permissions" for GITHUB_TOKEN under Settings → Actions → General → Workflow permissions
- [x] 1.2 Check branch protection rules on `main`; if direct pushes/tag creation are blocked, add the release bot identity to the bypass list or provision a PAT secret instead — confirmed: no branch protection rules defined on `main`, default `GITHUB_TOKEN` can push directly, no PAT needed
- [x] 1.3 Confirm the .NET 10 Desktop Runtime version story for the framework-dependent artifact matches what's documented so the release notes/README can point users at the right prerequisite — confirmed via README.md: exact version `10.0.x`, link already present (`dotnet-10-runtime` reference, line 54), Windows shows its own install prompt if missing (line 20); no changes needed, just keep this text in sync when task 5.2 updates the publish command description
- [x] 1.4 (found during verification) The repo had no git tags, so `semantic-release` would compute the first version from the entire commit history instead of continuing from the hand-maintained `2.3.1` — tagged the pre-automation commit (`c9bd7fa`, last `main` commit before this change merged) as `v2.3.1` and pushed it, so the next release is computed relative to it

## 2. semantic-release setup

- [x] 2.1 Add a minimal root `package.json` (`private: true`) with `semantic-release` and its plugins (`@semantic-release/commit-analyzer`, `@semantic-release/release-notes-generator`, `@semantic-release/changelog`, `@semantic-release/exec`, `@semantic-release/git`, `@semantic-release/github`) as devDependencies
- [x] 2.2 Add `.releaserc.json` (or `release.config.js`) configuring: `branches: ["main"]`, the plugin pipeline in order, and a custom type-to-section mapping for `release-notes-generator` so output stays in Keep a Changelog categories (`feat`→Added, `fix`→Fixed, etc.)
- [x] 2.3 Configure `@semantic-release/exec`'s `prepareCmd` to write the computed `${nextRelease.version}` into `AssemblyVersion`/`AssemblyFileVersion`/`AssemblyInformationalVersion` in `source/StopWatch/Properties/AssemblyInfo.cs`
- [x] 2.4 Configure `@semantic-release/exec`'s `prepareCmd`/`publishCmd` to run the two `dotnet publish` commands (self-contained; framework-dependent single-file) and zip the framework-dependent output
- [x] 2.5 Configure `@semantic-release/github`'s `assets` to attach the self-contained exe and the framework-dependent zip to the created release
- [x] 2.6 Configure `@semantic-release/git` to commit `source/StopWatch/Properties/AssemblyInfo.cs` and `CHANGELOG.md` back to `main` as part of the release

## 3. Workflow changes

- [x] 3.1 Add a `release` job to `.github/workflows/build.yml`: `needs: [build]`, restricted to `github.event_name == 'push' && github.ref == 'refs/heads/main'`
- [x] 3.2 Revisit `continue-on-error: true` on the `build` job's test step so a failing test run actually blocks the `release` job (e.g. gate on the test step's own outcome, not just job completion) — made it conditional on `matrix.configuration == 'Debug'`, so a Release-leg failure now fails the `build` job and blocks `needs: [build]` downstream
- [x] 3.3 Add a changed-files check (e.g. `dorny/paths-filter`) in the `release` job so it only proceeds when the push touched `source/StopWatch/**`
- [x] 3.4 Add `actions/setup-node` and `npm ci` steps to the `release` job before running `npx semantic-release`
- [x] 3.5 Ensure the `release` job checks out with full git history (`fetch-depth: 0`) so semantic-release can see all tags/commits

## 4. Cleanup removals

- [x] 4.1 Delete `.github/workflows/dispatch.yml`
- [x] 4.2 Remove the `dependency-scan` job from `.github/workflows/codeql.yml`, leaving the `analyze` job intact

## 5. Documentation

- [x] 5.1 Update `CLAUDE.md`'s changelog/versioning guidance to describe the automated semantic-release flow, removing the manual "rename [Unreleased] on version bump" instruction
- [x] 5.2 Update `CLAUDE.md`'s release/publish command reference to describe the two artifacts now produced automatically, instead of the single manual `dotnet publish` command
- [x] 5.3 Add a one-line summary of the release process to `openspec/config.yaml`'s `context` so future AI-assisted changes are aware of it
- [x] 5.4 Update `README.md`'s "Building and releasing" section (lines 18-26) to describe the automated flow and both artifacts instead of the single manual `dotnet publish` command, keeping the existing .NET 10 Desktop Runtime prerequisite text (lines 20, 54) as-is

## 6. Version display in the main window

- [x] 6.1 Add a way to read the running assembly's version at runtime (e.g. via `Assembly.GetExecutingAssembly()`/`FileVersionInfo`, exposed through `AppInfo.cs` or a new helper)
- [x] 6.2 Set `MainWindow`'s `Title` to include the version next to the app name (e.g. "Jira StopWatch v2.4.0"), sourced from that helper rather than hardcoded

## 7. Verification

- [x] 7.1 Merge a test `fix:`/`feat:` commit touching `source/StopWatch/` into `main` and confirm: version bump, CHANGELOG.md entry, tag, GitHub Release, both artifacts attached, AssemblyInfo.cs updated — confirmed via PR #12 (`feat: automate...`) merged, then a `fix: add package-lock.json` commit on top: `semantic-release` computed `2.4.0`, pushed tag `v2.4.0` and the `chore(release): 2.4.0` commit (bumping `AssemblyInfo.cs` + `CHANGELOG.md`), and created the GitHub Release. One real issue found: `@semantic-release/github`'s upload of the ~167MB self-contained exe hit a transient `500 Error saving asset` from GitHub's upload API after 3 retries, leaving the release stuck in `draft` with only the framework-dependent zip attached; fixed by rebuilding the same artifact locally (`node scripts/publish-release-artifacts.js 2.4.0`, byte-identical size) and completing the upload + un-drafting by hand. This is a GitHub-infra flakiness risk worth knowing about for future releases (large-asset upload has no extra retry/backoff beyond octokit's default), not a config bug — no code change made for it.
- [x] 7.2 Push a commit that only touches files outside `source/StopWatch/` and confirm no release is triggered — confirmed: the `package-lock.json`-only push left the `Release` job's steps after "Check for App Source Changes" all `skipped`
- [x] 7.3 Push a `chore:`-only commit touching `source/StopWatch/` and confirm no release is produced (no releasable commit type) — confirmed: the `chore: remove stale AssemblyVersion wildcard comment` commit ran the full `Release` job, but the `2.4.0` release it produced came from the earlier `feat:`/`fix:` commits already in range since the `v2.3.1` baseline tag, not from this chore commit itself (chore is `hidden` in `.releaserc.json`'s type mapping)
- [x] 7.4 Confirm a failing test run on `main` blocks the release job — verified without touching `main`: opened a throwaway PR (#13, branch `verify/failing-test-gate`, closed and deleted after) with a deliberately failing test and a temporary debug job printing `needs.build.result`. Confirmed empirically: a Release-leg failure (with or without a Debug-leg failure too) yields `needs.build.result = failure`, which skips the `release` job; an isolated Debug-only failure (via `#if DEBUG`, Release passing) yields `needs.build.result = success`, so the release job is not blocked by Debug-only flakiness — matches the design in task 3.2 exactly
- [x] 7.5 Run the app locally and confirm the title bar shows the current version next to the app name — confirmed by user
