# Proposal

## Why

Jira StopWatch has no extension mechanism, so team-specific features (for example importing time from an external SQLite database) would have to live in this repo. We want plugins: .NET assemblies that live in their own repositories, load from disk, show their own UI and reuse host pieces (Jira client, theme). This is also the spike that decides whether the approach is viable on the team's EntraID-managed machines, where self-contained publish is blocked and native plugin DLLs must be proven to load. Tracked in GitHub issue #35; #36 and #37 build on it.

## What Changes

- New public assembly `StopWatch.Plugin.Abstractions` (the only thing a plugin references): `IPlugin`, `IPluginHost`, a read-only issue-list facade with public types, a minimal `IJiraApi` facade, declarative commands, and an independent `ContractVersion`. No database types. Packaged as a private NuGet package for plugin repos.
- A composition root in the host: construction of `JiraClient`, `IssueJiraService` and the view models moves out of the `MainWindow` constructor so a `PluginHost` can receive them. No user-visible behavior change.
- A plugin loader: scans `plugins/<Id>/` folders (`plugin.json` + `<Id>.dll`), validates `contractVersion`, loads into the default `AssemblyLoadContext`, resolves managed and native dependencies from the plugin folder (compatible with `PublishSingleFile`), loads in deterministic order, and isolates every plugin failure.
- Plugin commands surfaced by the host: a new tray icon context menu and a "Plugins" entry in the main window; plugins open their own WPF windows owned by the main window, inheriting the theme. Public `IssueAdded` / `IssueRemoved` events.
- Auto-update keeps the plugins folder intact.
- Sample plugins in the solution, not shipped in releases: `Samples/HelloWorld` (template) and `Samples/HelloSqlite` (the native-dependency gate), plus an integration test that loads both with the real loader.
- `docs/plugins.md` documenting how to write, install and debug a plugin.
- Out of scope: Jira API expansion (#36), time-load pipeline and `SplitTime` (#37), sandboxing, signing, permissions, hot unload, plugin-to-plugin dependencies.

## Capabilities

### New Capabilities
- `plugin-contract`: the public, versioned surface plugins code against (lifecycle, host services, issue-list facade and events, Jira facade, command model).
- `plugin-loading`: discovery, validation, dependency resolution, ordering and failure isolation of plugins on disk, and the sample plugins that prove it end to end.
- `plugin-commands`: how plugin-contributed commands appear in the tray menu and main window, and how plugin windows integrate with the host (owner, theme).

### Modified Capabilities
- `auto-update`: applying an update must not remove or alter installed plugins.

## Impact

- New projects: `StopWatch.Plugin.Abstractions`, `Samples/HelloWorld`, `Samples/HelloSqlite` (added to `StopWatch.sln`); new `Microsoft.Data.Sqlite` package for the sample only.
- `source/StopWatch`: `MainWindow` construction refactored, new plugin host/loader code, tray `ContextMenu`, "Plugins" UI, `App.xaml.cs` hook for loading at startup, `UpdateApplier` apply script.
- Tests: new loader/contract tests and the two-sample integration test.
- CI: build must keep `TreatWarningsAsErrors`; samples must not be packaged by the release job; a pack/publish step for the Abstractions NuGet package.
- Docs: `docs/plugins.md`.
