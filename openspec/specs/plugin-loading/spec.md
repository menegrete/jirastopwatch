## Purpose

Defines how the application discovers, validates and loads plugins from disk, resolves their dependencies, and keeps a failing plugin from affecting the application or other plugins.

## Requirements

### Requirement: Plugin folder layout and manifest
Each plugin SHALL live in `plugins/<Id>/` containing `<Id>.dll` and a `plugin.json` with at least `id`, `name`, `version` and `contractVersion`. A folder with a missing or malformed manifest SHALL be reported as not loaded with the reason.

#### Scenario: Valid plugin folder
- **WHEN** the folder `plugins/HelloWorld/` contains `HelloWorld.dll` and a valid `plugin.json`
- **THEN** the plugin is a candidate for loading

#### Scenario: Missing manifest
- **WHEN** a plugin folder has no `plugin.json`
- **THEN** it is marked not loaded with a reason and the other plugins are unaffected

### Requirement: Plugins location
The application SHALL look for plugins in two folders, in this priority order: a `plugins` folder next to the executable, then a per-user `plugins` folder under the user's local application data. When neither exists, the application SHALL behave exactly as it does without plugin support.

#### Scenario: No plugins folder
- **WHEN** neither plugins folder exists
- **THEN** startup, tray and main window behave as before and no failure is shown or logged

#### Scenario: Plugins in either folder
- **WHEN** one plugin is in the folder next to the executable and another in the per-user folder
- **THEN** both are loaded

### Requirement: Duplicate plugin ids
When the same plugin `id` is found in more than one plugins folder, the loader SHALL load only the one from the higher-priority folder and SHALL report the other as not loaded because of the duplicate.

#### Scenario: Same id in both folders
- **WHEN** a plugin `Foo` exists in the folder next to the executable and in the per-user folder
- **THEN** the one next to the executable is loaded and the per-user one is listed as not loaded (duplicate)

### Requirement: Contract version validation
The loader SHALL reject a plugin whose declared `contractVersion` is not compatible with the host's `ContractVersion`, without loading its assembly, and SHALL record a clear message stating both versions.

#### Scenario: Incompatible contract
- **WHEN** a plugin declares a `contractVersion` the host does not support
- **THEN** the plugin is not loaded, its status shows both versions, and other plugins load normally

### Requirement: Deterministic load order
Plugins SHALL be loaded in order of `id`, compared ordinally ignoring case, regardless of file system enumeration order. Plugins SHALL NOT depend on each other or on load order.

#### Scenario: Ordering
- **WHEN** plugins with ids `beta`, `Alpha` and `charlie` exist
- **THEN** they are loaded in the order Alpha, beta, charlie

### Requirement: Dependency resolution from the plugin folder
The host SHALL resolve a plugin's managed dependencies and native libraries from that plugin's own folder, including when the application is published as a single-file executable. Plugin assemblies SHALL load in the application's default load context so WPF resources inside plugins work.

#### Scenario: Native dependency
- **WHEN** a plugin uses a library that loads a native DLL located in its folder
- **THEN** the native DLL is found and loaded from that folder

#### Scenario: WPF resources in plugin
- **WHEN** a plugin window uses XAML resources compiled into its assembly
- **THEN** the window renders correctly

### Requirement: Failure isolation
Loading and every invocation into a plugin SHALL be guarded so that an exception is contained. A failing plugin SHALL be marked not loaded (or its call skipped) with the reason logged and visible in the UI, SHALL contribute no commands if its load failed, and SHALL NOT prevent other plugins from loading nor cause the global crash dialog.

#### Scenario: Plugin throws on initialization
- **WHEN** a plugin throws during initialization
- **THEN** it is marked not loaded with the reason, others load, and no crash dialog appears

#### Scenario: Plugin throws in a command
- **WHEN** a plugin command action throws
- **THEN** the error is logged and shown as a message, and the application keeps running

### Requirement: Visible plugin status
The application SHALL let the user see each discovered plugin with its name, version and either loaded state or the reason it was not loaded.

#### Scenario: Broken plugin listed
- **WHEN** one plugin failed to load
- **THEN** the plugin status view lists it with the failure reason

### Requirement: Sample plugins prove the mechanism
The solution SHALL include sample plugins `HelloWorld` (no dependencies, opens a themed window, writes to its data directory, logs) and `HelloSqlite` (uses a SQLite library with a native component, runs an in-memory create/insert/select and shows the result). They SHALL NOT be included in release artifacts. An integration test SHALL load both with the real loader.

#### Scenario: Both samples load
- **WHEN** the integration test loads both samples
- **THEN** HelloWorld loads and exposes its command

#### Scenario: Sqlite sample fails
- **WHEN** HelloSqlite fails to load
- **THEN** HelloWorld still loads
