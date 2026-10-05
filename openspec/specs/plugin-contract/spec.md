## Purpose

Defines the small, stable, public surface that plugins compile against, so plugins live in separate repositories and never depend on the application executable or its internal types.

## Requirements

### Requirement: Plugins depend only on the Abstractions assembly
A plugin SHALL be buildable referencing only the `StopWatch.Plugin.Abstractions` assembly. The contract SHALL NOT expose host-internal types (such as issue view models) nor any database-related types.

#### Scenario: Plugin compiles without the executable
- **WHEN** a plugin project references only the Abstractions assembly
- **THEN** it can implement the plugin interface, read issues, call the Jira facade and contribute commands

### Requirement: Contract version is independent of the application version
The Abstractions assembly SHALL expose its own `ContractVersion`, which changes only when the contract changes and is unrelated to the application's release version.

#### Scenario: App release without contract change
- **WHEN** a new application version is released with no contract change
- **THEN** `ContractVersion` is unchanged and existing plugins remain compatible

### Requirement: Plugin lifecycle
A plugin SHALL be initialized once with an `IPluginHost` after being loaded and SHALL then be asked for the commands it contributes.

#### Scenario: Initialization
- **WHEN** a plugin is loaded successfully
- **THEN** the host calls its initialization with a host object before requesting its commands

### Requirement: Host services for plugins
The host object SHALL give a plugin access to the main window (for use as window owner), the UI dispatcher, a data directory private to that plugin, and a logger.

#### Scenario: Private data directory
- **WHEN** two plugins each ask the host for their data directory
- **THEN** each receives a different directory, created on demand, that is not shared with the other

### Requirement: Read-only issue list facade
The host SHALL expose the issues in the list and the currently active issue to plugins as public, read-only types, and SHALL raise `IssueAdded` and `IssueRemoved` events when issues are added to or removed from the list.

#### Scenario: Issue added
- **WHEN** the user adds an issue to the list
- **THEN** every initialized plugin subscribed to `IssueAdded` is notified with a public issue description

#### Scenario: Subscriber throws
- **WHEN** a plugin's event handler throws
- **THEN** the exception is logged, other plugins still receive the event and the host does not show a crash dialog

### Requirement: Minimal Jira facade without exposing credentials
The host SHALL expose a Jira facade limited to: reading an issue's summary, reading time tracking, adding a worklog and adding a comment. The API token and credentials SHALL NOT be reachable by plugins through the contract.

#### Scenario: Plugin adds a worklog
- **WHEN** a plugin calls the facade to add a worklog to an issue
- **THEN** the host performs the request with its own configured credentials and returns the result without revealing them

### Requirement: Declarative commands
A plugin SHALL contribute commands as data (id, title, icon, location) plus an action; the host SHALL decide how to render them.

#### Scenario: Command described by data
- **WHEN** a plugin returns a command with a title and location
- **THEN** the host renders it at that location using its own styling
