# Spec Delta

## Purpose

Defines how commands contributed by plugins are presented and invoked in the host UI, and how plugin windows integrate visually and structurally with the main window.

## ADDED Requirements

### Requirement: Tray icon context menu
The tray icon SHALL offer a context menu on right-click listing commands whose location is the tray, grouped by plugin. Existing tray behavior (click to restore) SHALL be unchanged.

#### Scenario: Commands in tray menu
- **WHEN** a loaded plugin contributes a tray command and the user right-clicks the tray icon
- **THEN** the menu shows that command and choosing it runs it

#### Scenario: No plugins
- **WHEN** no plugin contributes tray commands
- **THEN** the tray context menu contains no plugin entries

### Requirement: Plugins entry in the main window
The main window SHALL provide a "Plugins" entry listing plugin commands and plugin status. It SHALL NOT appear to change the window for users who have no plugins installed.

#### Scenario: Invoke from main window
- **WHEN** the user opens the Plugins entry and picks a command
- **THEN** the command runs

### Requirement: Plugin windows are owned by the main window and themed
Windows opened by plugins SHALL be able to use the main window as their owner and SHALL inherit the application theme through the shared theme resources.

#### Scenario: Theme inheritance
- **WHEN** a plugin window references the shared theme brushes dynamically
- **THEN** it follows the current application theme, including when the theme changes

### Requirement: No change without plugins
With no plugins installed the application SHALL behave exactly as before this capability existed.

#### Scenario: Clean install
- **WHEN** the application starts with no plugins folder
- **THEN** timers, the issue list, mini timer and tray behave as before
