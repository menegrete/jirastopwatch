# main-window-placement Specification

## Purpose

Defines how the main window remembers where the user left it — screen position
and maximized/normal state — so it reopens there instead of at a fixed default
location, while staying reachable even if the remembered screen is no longer
connected.

## Requirements

### Requirement: The main window remembers its position between runs

The main window SHALL persist its screen position (top-left location) when it
changes, and SHALL restore that position on the next application start.

#### Scenario: User moves the window and restarts the app

- **WHEN** the user drags the main window to a new position and then restarts
  the application
- **THEN** the main window reopens at that same position

#### Scenario: First run, nothing remembered yet

- **WHEN** the application starts with no position previously saved
- **THEN** the main window appears at its existing default position

### Requirement: The main window remembers its maximized state between runs

The main window SHALL persist whether it was maximized or in its normal state,
and SHALL restore that state on the next application start.

#### Scenario: User maximizes the window and restarts the app

- **WHEN** the user maximizes the main window and then restarts the
  application
- **THEN** the main window reopens maximized

#### Scenario: User leaves the window in its normal state and restarts

- **WHEN** the user restores the main window to its normal (non-maximized)
  state and then restarts the application
- **THEN** the main window reopens in its normal state, at its remembered
  position

### Requirement: A remembered position stays reachable across monitor changes

If the screen configuration changes between runs — a monitor is disconnected,
resized, or no longer present at the same coordinates — the main window SHALL
NOT restore to a position that would leave it invisible or unreachable.

#### Scenario: The monitor the window was on is no longer connected

- **WHEN** the application starts and the remembered position lies on a
  monitor that is not currently connected
- **THEN** the main window appears fully on a currently connected screen
  instead of at the unreachable remembered position

#### Scenario: The remembered position is partially off the current screen

- **WHEN** the application starts and the remembered position would place part
  of the window outside the working area of the screen it lies on
- **THEN** the main window is nudged so it lies fully within that screen's
  working area

### Requirement: Minimizing does not change the remembered state

Minimizing the main window SHALL be treated as a transient, in-session
condition, not a state to remember across restarts.

#### Scenario: Window is minimized when the application is closed

- **WHEN** the user closes the application while the main window is minimized
- **THEN** the position and maximized/normal state remembered are the ones
  from before it was minimized, not "minimized"
