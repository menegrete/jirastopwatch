# Plugins

Jira StopWatch can load **plugins**: .NET assemblies that live in their own repositories, are copied into a folder on disk, add commands to the tray menu and the main window, open their own windows and reuse the host's Jira session and theme.

Plugins are meant for you and your team. There is no sandbox, signing or permission model: a plugin runs with the same rights as the application. What the host does provide is **failure isolation** — a plugin that fails to load or throws is reported as "not loaded" (or its action is skipped) and never takes the application, or other plugins, down.

## Installing a plugin

Plugins are looked for in two places, in this order:

1. a `plugins` folder **next to `StopWatch.exe`**;
2. `%LocalAppData%\StopWatch\plugins`, a per-user folder outside the install directory.

Either has the same layout:

```
plugins\<Id>    plugin.json
    <Id>.dll
    (the plugin's own dependencies, if any)
```

Copy the plugin's build output into a folder named after its id and restart the application. Use the per-user folder when the application lives somewhere you cannot write to (such as `Program Files`); use the folder next to the exe when your environment restricts running code from user-profile paths. Auto-update leaves both alone. If the same id is in both folders, the one next to the exe loads and the other is listed as not loaded (duplicate). With neither folder present the application behaves exactly as it does without plugin support.

Open the status bar's **Plugins** button (shown only when the folder has something in it) to run a plugin's commands or to see **Plugin status**, which lists every plugin and the reason for any that did not load. Plugin commands that ask for it also appear in the **tray icon's right-click menu**.

Each plugin gets its own data folder, `%LocalAppData%\StopWatch\plugin-data\<Id>\`, available as `host.DataDirectory`.

## The manifest (`plugin.json`)

```json
{
  "id": "HelloWorld",
  "name": "Hello World",
  "version": "1.0.0",
  "contractVersion": "1.0"
}
```

| Field | Meaning |
|---|---|
| `id` | Must equal the folder name and the assembly file name (`<id>.dll`). Plugins load in order of `id`, compared ordinally ignoring case. |
| `name` | Shown in menus and the status list. |
| `version` | The plugin's own version, for display. |
| `contractVersion` | The version of the plugin contract the plugin was built against. |

## Contract versioning

The contract is the public surface of `StopWatch.Plugin.Abstractions`. Its version is `PluginContract.ContractVersion` and is **independent of the application's version** — the application is released automatically by `semantic-release`, the contract changes only when the Abstractions assembly does.

A plugin is compatible when its `contractVersion` has the **same major** as the host and a **minor no greater** than the host's. Additive changes bump the minor, breaking changes bump the major. An incompatible plugin is not loaded and its status says which versions are involved. The NuGet package version (`<Version>` in the Abstractions csproj) must be kept in step with `ContractVersion`.

| Contract | Added |
|---|---|
| 1.0 | Commands, issue list, `IJiraApi` (summary, time tracking, worklog, comment) |
| 1.1 | Subtasks (`GetSubtasksAsync`, `CreateSubtaskAsync`), a worklog overload with the estimate, and time loading (`IPluginHost.TimeLoad`, `IPluginHost.TimeLoader`). Everything in 1.1 is additive: a plugin built against 1.0 loads and works on a 1.1 host, while one declaring 1.1 is not loaded by a 1.0 host. |

## Writing a plugin

Start from `source/Samples/HelloWorld`, the template. A plugin is a class library (`net10.0-windows`, `UseWPF` if it has windows) that:

1. references **only** `StopWatch.Plugin.Abstractions` — never `StopWatch.exe`. In a plugin repository use the private NuGet package from GitHub Packages with `ExcludeAssets="runtime"` (the host already has the assembly loaded; the plugin must not ship its own copy);
2. contains exactly one public class implementing `IPlugin` with a parameterless constructor;
3. ships a `plugin.json`.

```csharp
public class MyPlugin : IPlugin
{
    private IPluginHost host;

    public void Initialize(IPluginHost host) => this.host = host;

    public IEnumerable<PluginCommand> GetCommands()
    {
        yield return new PluginCommand("open", "Open my window", Open, PluginCommandLocation.Both);
    }

    private void Open() => new MyWindow { Owner = host.MainWindow }.Show();
}
```

### Referencing the contract package

`StopWatch.Plugin.Abstractions` is published as a NuGet package to this repository's GitHub Packages feed. GitHub Packages requires authentication even to read, so a plugin repository needs a feed entry and a token.

1. Create a personal access token (classic) with the **`read:packages`** scope. It must belong to someone with access to this repository. Keep it out of the repository: put it in an environment variable, for example `GITHUB_PACKAGES_TOKEN`.
2. Add a `nuget.config` next to the plugin's solution (it is safe to commit, because it holds no secret, only the name of the variable):

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration>
     <packageSources>
       <add key="stopwatch" value="https://nuget.pkg.github.com/menegrete/index.json" />
     </packageSources>
     <packageSourceCredentials>
       <stopwatch>
         <add key="Username" value="%GITHUB_USERNAME%" />
         <add key="ClearTextPassword" value="%GITHUB_PACKAGES_TOKEN%" />
       </stopwatch>
     </packageSourceCredentials>
   </configuration>
   ```

   NuGet expands `%VARIABLE%` references in `nuget.config`, so set `GITHUB_USERNAME` (your GitHub user name) and `GITHUB_PACKAGES_TOKEN` in the environment where you build. In GitHub Actions of another repository, pass a token with `read:packages` as a secret, since the default `GITHUB_TOKEN` of that repository cannot read this one's packages.
3. Reference the package with `ExcludeAssets="runtime"`, so the plugin does not copy the contract assembly into its output (the host already has it loaded):

   ```xml
   <PackageReference Include="StopWatch.Plugin.Abstractions" Version="1.0.0" ExcludeAssets="runtime" />
   ```

Use the package version that matches the `contractVersion` you declare in `plugin.json`: package `1.0.x` is contract `1.0`.

### Lifecycle

The host loads the plugin, calls `Initialize(host)` once on the UI thread, then `GetCommands()` once. There is no unload. Plugins must not depend on each other or on load order.

### What `IPluginHost` offers

- `MainWindow` — use as `Owner` of your windows.
- `Dispatcher` — the UI dispatcher.
- `DataDirectory` — a folder private to the plugin, created on first use.
- `Logger` — writes to the application log, tagged with your id.
- `Issues` — read-only issue list: `GetIssues()`, `GetActiveIssue()`, and the `IssueAdded` / `IssueRemoved` events. These are plain public types (`PluginIssue`), never the host's view models.
- `Jira` — `GetSummaryAsync`, `GetTimeTrackingAsync`, `AddWorklogAsync`, `AddCommentAsync`, `GetSubtasksAsync` and `CreateSubtaskAsync`, run with the host's own session. The API token never reaches a plugin. Methods report failure with `null`/`false` rather than exceptions. `AddWorklogAsync` has an overload that takes the estimate update (`PluginEstimateUpdate` and a value), so a plugin that takes over a load can honor what the user chose; without it the estimate is updated automatically. `CreateSubtaskAsync(parentKey, summary)` creates a subtask of the first subtask type the parent's project offers, and returns its key or `null`. **Keep that key**: if the answer is lost and you create it again, Jira has two subtasks.
- `TimeLoad` — register a replacement handler for time loads and observe their outcome (see [Time loading](#time-loading)).
- `TimeLoader` — load time through the host's pipeline (see [Time loading](#time-loading)).

### Commands

A command is data (`Id`, `Title`, optional `Icon` glyph, `Location`) plus an action; the host decides how to draw it. `Location` is `Tray`, `MainWindow` or `Both`. If your action throws, the host logs it and shows a message; the application keeps running.

### Windows and theme

Open windows with `Owner = host.MainWindow`. The host applies its theme brushes at application level, so a plugin window that references them dynamically follows the theme, including when the user changes it:

```xml
<Window Background="{DynamicResource Background}">
    <TextBlock Foreground="{DynamicResource Text}" ... />
```

Plugin assemblies load into the application's default load context, so XAML compiled into them (pack URIs, BAML) works normally.

## Time loading

When the user posts an issue's time, the host shows its usual worklog dialog first, unchanged. Only after they confirm it does the load run through a **pipeline**: replacement handlers, then the standard load if no handler took it, then observers. A plugin can take part in two ways, and in neither does it change the dialog.

```
worklog dialog (host) ─▶ handlers, by plugin id ─▶ standard load ─▶ observers (After)
                              │                         ▲
                              └── Declined / Failed ────┘ (when nothing was written)
```

### Replacing the load: `TimeLoad.RegisterInsteadOf`

A plugin registers **one** handler, usually in `Initialize`. It receives a `TimeLoadRequest` with the issue key, start time, elapsed time (the total the user confirmed), comment, estimate update and value, the `Source` of the load, and a `Jira` API. It runs on the UI thread, may open its own dialog, and **is never timed out**: it may be waiting for a person. Only an exception counts as a failure. It answers with an `InsteadOfResult`:

| Answer | Means |
|---|---|
| `Declined()` | Not for me. Must not have written anything. |
| `Handled(timeLoaded)` | I loaded it, comment included. The host posts nothing itself. |
| `Cancelled()` | The user closed my dialog. Nothing was loaded. Different from declining: declining lets the host load the whole total, cancelling does not. |
| `Failed(reason)` | I could not load it. A thrown exception, or a handler that returns no task or no result, counts the same. |

What the host does depends on the answer **and on what the handler wrote** through `request.Jira`, a Jira API that counts the worklogs and subtasks Jira accepted during this invocation and adds up the time of those worklogs (the time the host *measures* as loaded):

| Answer | Writes | The host | The timer |
|---|---|---|---|
| `Declined` | 0 | Runs the standard load | Reset if it succeeds |
| `Declined` | more than 0 | Contract violation: treated as a failure with writes | Reduced by the measured time |
| `Handled`, `timeLoaded` equals the confirmed total | any | Does not run the standard load | Reset |
| `Handled`, `timeLoaded` differs | any | Does not run the standard load; tells the user, in a message box, what the plugin declared, what was confirmed, what reached Jira and what the timer keeps | Reduced by the measured time |
| `Cancelled` | 0 | Nothing: no load, no notice | Kept |
| `Cancelled` | more than 0 | Treated as a failure with writes | Reduced by the measured time |
| `Failed` or exception | 0 | **Falls back** to the standard load, with a non-modal notice in the status bar and a log entry naming the plugin | Reset if the fallback succeeds |
| `Failed` or exception | more than 0 | **No fallback**: a message box says how many writes were made, how much time was loaded and how much the timer keeps | Reduced by the measured time |

The fallback is always visible so a broken plugin is not masked. A failure after writing cannot fall back, because the standard load would load the same time twice.

### The timer after a partial load, and trying again

When time reached Jira but the load did not finish, the timer is **not reset: it is reduced by the time the host measured**, never below zero. It keeps running if it was running, keeps its recorded start time, and is reduced from the value it holds when the load ends, so time that accrued while your dialog was open is not lost. The comment and the estimate stay as the user left them.

That is what makes a retry safe without any help from the plugin: the user posts again, the dialog shows only the time that remains, and a handler (or, if it fails before writing anything new, the standard load it falls back to) loads just that. **A handler does not need to recognize that it is seeing the same load again**, and must not try: the start time of a timer edited by hand is recalculated on every dialog and the elapsed time of a running timer keeps growing, so neither is stable between attempts.

A few things follow:

- The time is measured from what Jira accepted through `request.Jira`, never from what the plugin declares, so a plugin cannot make the host subtract more or less than was written. Worklogs posted through `host.Jira` are not measured, so the timer would be reduced by less than was written and the retry could load some of it twice.
- The proposed start time of the retry is the timer's recorded start (or "now minus what remains" when that is earlier); the user can edit it in the dialog. A comment or an estimate of "Reduce by" is applied again to what remains; the dialog shows both so the user can adjust them.
- Each attempt rounds the remaining time up to a whole minute, so a chain of partial loads can load up to a minute more than was tracked.
- A subtask created before the failure stays: it is one more subtask of the issue, and a retry can give it time.

The `timeLoaded` check exists so that a plugin cannot lose time silently: if it loads 5 of the 7 minutes and says it is done, the timer is not reset. The host does not claim to verify what was actually written, only what the plugin declares.

**Write through `request.Jira`, not through `host.Jira`.** Writes made through `host.Jira` inside a handler are not counted, so a later failure would look like a failure before writing, fall back, and load the time twice.

Several plugins can register handlers. They are asked in order of plugin id (ordinal, ignoring case) and the first that does not decline wins. A failure from that handler is **not** passed on to the next one. A plugin is never asked about loads it started itself, and a load started from inside a handler (through `ITimeLoader`) does not consult handlers at all, so loads cannot loop.

The user is told about problems only for loads they started. For a load a plugin started through `ITimeLoader`, the outcome goes back to that plugin and is logged, so a plugin that imports many entries cannot flood the user with dialogs.

While a handler waits, the host ignores a second request to load the same issue, and the wait cursor is shown only while the host's own load runs.

### Two ways to post time

- **`IJiraApi.AddWorklogAsync`** is the **raw** operation. It runs no handlers and notifies no observers. Use it inside your own handler to write your worklogs without intercepting yourself.
- **`ITimeLoader.LoadAsync`** goes through the whole pipeline with `Source` = `plugin:<your id>`. Use it when other plugins should be able to react to your load. Your own handler is not asked about it. It does not touch the timer of any issue in the list: the result (`Success`, `Reason`) is for you.

### Observing loads: `TimeLoad.After`

`After` is raised after every load that reaches the pipeline, whoever handled it, including cancelled loads. Its `TimeLoadedEventArgs` say what was loaded (the confirmed total when it succeeded, otherwise the time the host measured), who handled it (`HandledBy` is the plugin id, or `null` when the host did), the outcome, the reason and the number of writes made. An observer cannot veto or change the load, and one that throws is logged without affecting the load or the other observers.

### Sharing minutes

Jira takes worklogs in whole minutes, so a plugin that splits time must make the shares add up to the total exactly. Giving the remainder one minute at a time to the first shares, 7 minutes over 3 subtasks is 3, 2 and 2.

## Dependencies, including native ones

A plugin may bring its own libraries, **copied loose into its folder**:

- **Managed** dependencies are found in the plugin's folder (using its `.deps.json` when present). Set `CopyLocalLockFileAssemblies` to `true` in the plugin project so package dependencies land next to it.
- **Native** libraries are found in the folder, or in `runtimes\win-<arch>\native\`, for the plugin and for every assembly it ships. `Samples/HelloSqlite` is the worked example: it uses `Microsoft.Data.Sqlite` (no database types appear in the contract) and runs a query, which forces the native `e_sqlite3.dll` to load.

Avoid redistributing assemblies the host already ships (for example RestSharp or MaterialDesignThemes): in the default load context the host's copy wins.

This works with the single-file published application, because plugins are loose files on disk. It is also the check to run on a new machine: install `HelloSqlite` and run its command; if it loads there, plugins with private dependencies will.

## When something goes wrong

- The **Plugin status** list shows each plugin and why it was not loaded: missing or malformed `plugin.json`, an `id` that does not match the folder, an incompatible contract version, a missing or corrupt dll, no (or more than one) `IPlugin`, or an exception during initialization.
- The same reason goes to the application log (turn on "Enable debug logging" in Settings).
- Limits of the isolation: exceptions thrown on threads a plugin creates itself, or from async-void code it starts, are outside what the host can guard and may still reach the global error handler.

## Debugging a plugin from its own repository

1. Build the plugin so its output folder holds `plugin.json`, `<Id>.dll` and its dependencies.
2. Point the build at the plugins folder, for example a post-build step that copies the output to `%LocalAppData%\StopWatch\plugins\<Id>\`.
3. In the plugin project's debug settings, launch `StopWatch.exe` as the external program (or attach to the running process). Breakpoints in the plugin work once its assembly is loaded.
4. A plugin cannot be reloaded: restart the application after each rebuild.

## The samples

`source/Samples/` holds three plugins that are part of the solution but **not** of the release (the release publishes `source/StopWatch/StopWatch.csproj` only, which does not reference them):

- `HelloWorld` — no dependencies; a command that opens a themed window, writes to its data directory and logs. The template for a new plugin.
- `HelloSqlite` — the native-dependency check described above.
- `SplitTime` — a replacement handler for time loads. When the user posts an issue that has subtasks, it asks how to share the time among them (an even split to start from, minutes you can edit, and an optional new subtask) and writes one worklog per share through `request.Jira`. It declines loads from other plugins and issues without subtasks, cancels when the dialog is closed, fails before writing when it cannot read the subtasks (the host falls back), and keeps no memory of earlier attempts: after a failure halfway the host leaves in the timer only what is not in Jira, so the retry is simply a new split of the remainder. The shares always add up to the total.

Integration tests load all of them with the real loader and check that a broken plugin does not affect the others; `SplitTimeTest` runs the `SplitTime` handler through the host's pipeline for every way a load can end.
