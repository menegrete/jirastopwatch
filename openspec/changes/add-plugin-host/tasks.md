## 1. Abstractions assembly

- [x] 1.1 Create `StopWatch.Plugin.Abstractions` project (net10.0-windows, WPF) and add it to `StopWatch.sln`; keep `TreatWarningsAsErrors`
- [x] 1.2 Define `ContractVersion` (major.minor) and the compatibility rule
- [x] 1.3 Define `IPlugin`, `IPluginHost` (main window, dispatcher, per-plugin data directory, logger)
- [x] 1.4 Define public issue types, `IPluginIssueList` (issues, active issue, `IssueAdded`/`IssueRemoved`)
- [x] 1.5 Define minimal `IJiraApi` (summary, time tracking, add worklog, add comment) with no credential members
- [x] 1.6 Define `PluginCommand` (id, title, icon, location, execute)

## 2. Composition root (no behavior change)

- [x] 2.1 Extract construction of `JiraClient`, `IssueJiraService`, `IssueListViewModel`, `ActiveTimerViewModel` out of the `MainWindow` constructor into a composition class
- [ ] 2.2 Verify existing tests pass and smoke-test the app unchanged

## 3. Loader and host adapters

- [x] 3.1 Implement manifest model/parsing for `plugin.json` with validation errors as "not loaded" reasons
- [x] 3.2 Implement contract version compatibility check with a clear message
- [x] 3.3 Implement discovery from the folder next to the executable and the `%LocalAppData%` plugins folder (exe-side first), ordinal-ignore-case ordering by id, duplicate ids reported, no-op when no folder exists
- [x] 3.4 Implement load in the default `AssemblyLoadContext` with managed (`AssemblyDependencyResolver`/`Resolving`) and native (`SetDllImportResolver`, incl. `runtimes\win-x64\native`) resolution
- [x] 3.5 Implement the guarded-call helper and plugin status model (loaded / failed + reason, logged with plugin id)
- [x] 3.6 Implement `PluginHost`, `IJiraApi` adapter over `IJiraOperations`/`IssueJiraService`, and issue-list adapter with per-subscriber isolated events
- [x] 3.7 Load plugins at startup from `App`/`MainWindow` without triggering the global crash dialog on plugin failure

## 4. Sample plugins and gate

- [x] 4.1 Create `Samples/HelloWorld` (themed window with `Owner`, writes to data directory, logs, `plugin.json`) and wire its output into a plugin-shaped folder
- [x] 4.2 Add `Microsoft.Data.Sqlite` to `Directory.Packages.props`; create `Samples/HelloSqlite` (in-memory create/insert/select shown in a window)
- [x] 4.3 Exclude samples from release publish/packaging
- [x] 4.4 Gate: publish framework-dependent single-file, install both samples, verify both load and run on an EntraID machine, from the folder next to the executable and from `%LocalAppData%`; if blocked, revisit plugin location before continuing

## 5. Commands and UI

- [x] 5.1 Add tray icon context menu built from tray-located plugin commands, keeping existing click behavior
- [x] 5.2 Add "Plugins" entry in the main window listing commands and plugin status (hidden/inert when no plugins)
- [x] 5.3 Verify plugin windows inherit theme via `DynamicResource` on `ThemeBrushes` and follow theme changes

## 6. Auto-update

- [x] 6.1 Add `/XD plugins` (or equivalent) to the framework-dependent apply script so a plugins folder beside the exe survives
- [x] 6.2 Update `UpdateApplier` script tests and verify plugins in AppData are untouched

## 7. Tests

- [x] 7.1 Unit tests: manifest validation, contract version compatibility, load ordering
- [x] 7.2 Unit tests: failure isolation (throwing init, command, event handler)
- [x] 7.3 Integration test: load HelloWorld and HelloSqlite with the real loader; HelloWorld loads even if HelloSqlite fails
- [x] 7.4 Confirm `dotnet build StopWatch.sln` and `dotnet test StopWatch.sln --settings .runsettings` pass

## 8. Packaging and docs

- [x] 8.1 Make Abstractions packable and add a GitHub Packages publish step
- [x] 8.2 Write `docs/plugins.md` (folder layout, manifest, lifecycle, commands/windows, theme, `DataDirectory`, contract versioning, native dependencies, debugging from a plugin repo)
