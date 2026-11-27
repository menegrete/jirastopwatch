# Spec Delta

## MODIFIED Requirements

### Requirement: Host services for plugins
The host object SHALL give a plugin access to the main window (for use as window owner), the UI dispatcher, a data directory private to that plugin, and a logger. The main window and the UI dispatcher SHALL be the cross-platform UI types the host itself uses, so that a plugin's windows work on every supported platform.

#### Scenario: Private data directory
- **WHEN** two plugins each ask the host for their data directory
- **THEN** each receives a different directory, created on demand, that is not shared with the other

#### Scenario: Plugin window on Linux
- **WHEN** a plugin opens a window owned by the host's main window while the application runs on Linux
- **THEN** the window is shown with the active theme and stays above the main window

## REMOVED Requirements

### Requirement: Contract 1.1 is additive
**Reason**: Contract 2.0 replaces the WPF types in the host services, so no 1.x plugin loads on a 2.0 host.
**Migration**: Rebuild the plugin against the 2.0 Abstractions assembly and declare `contractVersion` 2.0 in `plugin.json`.

### Requirement: Contract 1.2 is additive
**Reason**: Superseded by "Contract 2.0 is a breaking version"; the contract is no longer 1.2.
**Migration**: Rebuild the plugin against the 2.0 Abstractions assembly and declare `contractVersion` 2.0 in `plugin.json`.

## ADDED Requirements

### Requirement: Contract 2.0 is a breaking version
The contract version SHALL be 2.0. Contract 2.0 SHALL keep every capability of contract 1.2 except that the UI types exposed by the host are the cross-platform ones. A plugin declaring any 1.x contract SHALL NOT load on a 2.0 host, and a plugin declaring 2.0 SHALL NOT load on a 1.x host.

#### Scenario: Plugin built against 1.2 on a 2.0 host
- **WHEN** a 2.0 host finds a plugin declaring contract 1.2
- **THEN** the plugin is reported as not loaded, its status shows both versions, and other plugins load normally

#### Scenario: Plugin built against 2.0 on a 1.2 host
- **WHEN** a 1.2 host finds a plugin declaring contract 2.0
- **THEN** the plugin is reported as not loaded because its major differs from the host's

#### Scenario: Capabilities kept from 1.2
- **WHEN** a plugin built against 2.0 creates a subtask by type name, reads time tracking and adds a worklog
- **THEN** each operation behaves as it did in contract 1.2
