# Design

## Context

See proposal.md for motivation. Current state that shapes the approach:

- `MainWindow`'s constructor builds `JiraClient`, `IssueJiraService`, `IssueListViewModel` and `ActiveTimerViewModel` itself; nothing else can obtain them. Most types are `internal`, with `InternalsVisibleTo` only for tests.
- The tray `NotifyIcon` (created in `MainWindow`) handles only `Click`; there is no context menu.
- `App.xaml.cs` installs the global crash handlers (`DispatcherUnhandledException`, `AppDomain.UnhandledException`) that show the crash dialog.
- Releases are published single-file and framework-dependent in the team's environment (EntraID blocks self-contained). `UpdateApplier` applies a framework-dependent update with `robocopy /MIR` into the install directory, which deletes anything the new build does not contain, so a `plugins` folder next to the exe would be wiped.
- The release job packages `source/StopWatch/**`; samples and the Abstractions package must not leak into the release zips.

## Goals / Non-Goals

**Goals:**
- A plugin can be written in another repo against one small assembly and load on the team's machines, including with a native dependency.
- A broken or incompatible plugin never takes the host or other plugins down.
- Zero behavior change without plugins.

**Non-Goals:**
- Sandboxing, signing, permission model, hot unload, plugin-to-plugin dependencies.
- Jira API growth (#36) and time-load pipeline (#37); the facades are shaped so those can extend them additively.

## Decisions

**1. Separate public assembly `StopWatch.Plugin.Abstractions` (net10.0-windows, WPF enabled for `Window`/`Dispatcher` types).** Plugins never reference `StopWatch.exe`. Alternative: make host types public — rejected, it freezes internals and leaks view models. The host owns adapters mapping internal types to public DTOs (`PluginIssue` etc.).

**2. `ContractVersion` is a constant in Abstractions, major.minor.** Compatible when majors are equal and the plugin's minor is not greater than the host's. Additive changes bump minor; breaking changes bump major. Independent of semantic-release, which versions the exe.

**3. Composition root: a small `AppComposition` class built in `App.OnStartup`-time or at `MainWindow` creation, returning the wired `JiraClient`, `IssueJiraService`, `IssueListViewModel`, `ActiveTimerViewModel`.** `MainWindow` receives them (constructor parameter, with the existing construction preserved behind it). Alternative: a DI container — rejected as disproportionate for four objects. The refactor is verified by existing tests plus manual smoke; no behavior change.

**4. Plugin locations: both a `plugins` folder next to the executable and `%LocalAppData%\StopWatch\plugins\<Id>\`, in that priority order.** Neither is clearly right for every environment: next to the exe is the simplest to explain and is more likely to be allowed by AppLocker-style policies that restrict executable code in user-profile paths; AppData needs no write access to the install directory (it may be `Program Files`) and survives updates by construction. Supporting both lets the team find out on their EntraID machines which works, without a later change. An id found in both is loaded from the exe-side folder only and the other is reported as a duplicate (two assemblies of the same name cannot coexist in the default load context anyway). The update script gets `/XD plugins` so the robocopy `/MIR` of a framework-dependent update does not mirror the exe-side folder away; the self-contained update only swaps the exe. Per-plugin data lives apart, in `%LocalAppData%\StopWatch\plugin-data\<Id>\`, regardless of where the plugin itself is installed. The HelloSqlite gate checks both locations; if both are blocked by policy, the location question is reopened before continuing.

**5. Loading in the default `AssemblyLoadContext`.** Required so WPF pack URIs and BAML in plugin assemblies resolve. Consequence: no unload, and dependency version conflicts with host assemblies resolve to the host's copy. Managed dependencies: a per-plugin `AssemblyDependencyResolver` (reads the plugin's `.deps.json` if present) hooked via `AssemblyLoadContext.Default.Resolving`, falling back to probing the plugin folder; scoped to assemblies requested by that plugin's assembly. Native: `NativeLibrary.SetDllImportResolver(pluginAssembly, ...)` per plugin assembly, plus one for assemblies the plugin ships (e.g. `SQLitePCLRaw`), probing `<folder>` and `<folder>\runtimes\win-x64\native`. These APIs are independent of single-file bundling because plugin files are loose on disk.

**6. Isolation via a `PluginHostService` boundary.** Every call into plugin code (load, `Initialize`, `GetCommands`, command action, event delivery) goes through one guarded helper that catches `Exception`, logs it with the plugin id, and records status. Failure during load leaves the plugin in `Failed(reason)` and drops its commands. Event raising iterates subscribers individually so one throwing handler cannot stop the rest. Unhandled exceptions on plugin-created threads/dispatcher callbacks cannot be fully contained; documented in `docs/plugins.md`.

**7. Declarative commands.** `PluginCommand { Id, Title, Icon (optional), Location (Tray | MainWindow), Execute }`. The host builds the tray `ContextMenuStrip` (WinForms, since the `NotifyIcon` lives there) and a WPF "Plugins" button/menu in `MainWindow`. The same status model backs a plugin list showing loaded/failed reasons. Menu is built after load, once; no live re-registration needed (no hot reload).

**8. Issue facade.** `IPluginIssueList` exposes `Issues` (snapshot of `PluginIssue` records), `ActiveIssue`, `IssueAdded`, `IssueRemoved`. The adapter subscribes to `IssueListViewModel.Issues.CollectionChanged` and translates; it is created only when at least one plugin loaded.

**9. `IJiraApi` facade wraps `IJiraOperations`/`IssueJiraService`** with summary, time tracking, add worklog, add comment, returning plain results. The token stays inside the host's `JiraClient`; the facade has no credential members.

**10. Samples under `source/Samples/`** in `StopWatch.sln`, referencing Abstractions by project. They build to a plugin-shaped output folder (`<Id>.dll`, `plugin.json`, dependencies copied). Not referenced by `StopWatch.csproj` and excluded from the release publish steps. `Microsoft.Data.Sqlite` is added to `Directory.Packages.props` and used only by `HelloSqlite`. The integration test (in `StopWatchTest`) copies the built sample outputs to a temp plugins folder and runs the real loader; it avoids opening WPF windows (checks load status and command presence, and executes the SQLite logic via a non-UI entry point).

**11. Abstractions NuGet package.** `Pack`-enabled project; a workflow step publishes to GitHub Packages on release. Versioned from `ContractVersion`, not the exe version. Wiring into the existing `release` job is kept minimal and separate from the exe version bump.

## Risks / Trade-offs

- [EntraID/policy blocks loading unsigned DLLs or native DLLs from AppData] → HelloSqlite is the gate and runs first; location is revisited before building the UI layer.
- [Default load context means host/plugin dependency version conflicts] → Plugins should avoid redistributing assemblies the host ships (RestSharp, MaterialDesign); documented. Accepted for WPF compatibility.
- [A plugin can still crash the process via its own threads] → Documented limitation; no sandbox per scope.
- [Composition refactor regresses the main window] → Keep construction order and arguments identical; run the full test suite and smoke-test the app.
- [`robocopy /XD plugins` changes the apply script] → Covered by an updated `UpdateApplier` script test.
- [Contract frozen too early] → Facades kept minimal; #36 extends additively with a minor bump.
