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

### Lifecycle

The host loads the plugin, calls `Initialize(host)` once on the UI thread, then `GetCommands()` once. There is no unload. Plugins must not depend on each other or on load order.

### What `IPluginHost` offers

- `MainWindow` — use as `Owner` of your windows.
- `Dispatcher` — the UI dispatcher.
- `DataDirectory` — a folder private to the plugin, created on first use.
- `Logger` — writes to the application log, tagged with your id.
- `Issues` — read-only issue list: `GetIssues()`, `GetActiveIssue()`, and the `IssueAdded` / `IssueRemoved` events. These are plain public types (`PluginIssue`), never the host's view models.
- `Jira` — `GetSummaryAsync`, `GetTimeTrackingAsync`, `AddWorklogAsync`, `AddCommentAsync`, run with the host's own session. The API token never reaches a plugin. Methods report failure with `null`/`false` rather than exceptions.

### Commands

A command is data (`Id`, `Title`, optional `Icon` glyph, `Location`) plus an action; the host decides how to draw it. `Location` is `Tray`, `MainWindow` or `Both`. If your action throws, the host logs it and shows a message; the application keeps running.

### Windows and theme

Open windows with `Owner = host.MainWindow`. The host applies its theme brushes at application level, so a plugin window that references them dynamically follows the theme, including when the user changes it:

```xml
<Window Background="{DynamicResource Background}">
    <TextBlock Foreground="{DynamicResource Text}" ... />
```

Plugin assemblies load into the application's default load context, so XAML compiled into them (pack URIs, BAML) works normally.

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

`source/Samples/` holds two plugins that are part of the solution but **not** of the release:

- `HelloWorld` — no dependencies; a command that opens a themed window, writes to its data directory and logs. The template for a new plugin.
- `HelloSqlite` — the native-dependency check described above.

An integration test (`PluginSamplesIntegrationTest`) loads both with the real loader and checks that a broken plugin does not affect the others.
