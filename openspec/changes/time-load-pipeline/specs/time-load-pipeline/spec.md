# Spec Delta

## Purpose

Defines how the host loads time into Jira once the user has confirmed it, giving plugins a controlled chance to replace the write or observe it, with a safe fallback to the original behavior when a plugin fails.

## ADDED Requirements

### Requirement: Time load runs through a pipeline after the host dialog
When the user confirms a time load in the host's worklog dialog, the host SHALL run the load through a pipeline made of a replacement stage, the original load and an observation stage. The host's dialog SHALL be shown unchanged and before any plugin is consulted. Saving the worklog for later SHALL NOT run the pipeline.

#### Scenario: Plugin is consulted after the user confirms
- **WHEN** the user confirms the total, comment and estimate in the worklog dialog
- **THEN** the replacement stage receives exactly those confirmed values

#### Scenario: Save for later
- **WHEN** the user chooses to save the worklog for later instead of posting it
- **THEN** no plugin is consulted and nothing is loaded

### Requirement: Behavior without plugins is unchanged
When no plugin takes part in a load, the host SHALL post the comment and the worklog exactly as it did before the pipeline existed, and SHALL reset the timer only if everything was posted.

#### Scenario: No plugins installed
- **WHEN** the user confirms a time load and no plugin is installed
- **THEN** the original load runs and the timer is reset only if it succeeded

#### Scenario: Original load fails
- **WHEN** the original load fails
- **THEN** the timer is not reset

### Requirement: A declining handler leaves the load to the host
When every consulted handler declines and made no writes, the host SHALL run the original load.

#### Scenario: Handler declines
- **WHEN** a handler answers that the load is not for it
- **THEN** the original load runs as if no plugin existed

### Requirement: A handled load replaces the original and is validated
When a handler reports the load as handled, the host SHALL NOT run the original load and SHALL NOT post the comment itself. The handler reports the time it loaded; if that equals the total the user confirmed, the host SHALL reset the timer. Otherwise the host SHALL NOT reset the timer and SHALL show a visible, non-modal notice stating the time loaded and the time confirmed.

#### Scenario: Handled completely
- **WHEN** a handler reports the load as handled with a loaded time equal to the confirmed total
- **THEN** the original load does not run and the timer is reset

#### Scenario: Handled but time does not match
- **WHEN** a handler reports the load as handled with a loaded time different from the confirmed total
- **THEN** the original load does not run, the timer is not reset and a visible notice says how much was loaded and how much was confirmed

### Requirement: A cancelled load changes nothing
When a handler reports that the user cancelled in the plugin's own dialog and made no writes, the host SHALL NOT run the original load, SHALL NOT reset the timer and SHALL NOT show a failure notice. Cancelling is different from declining: it SHALL NOT cause the full total to be loaded.

#### Scenario: User closes the plugin's dialog
- **WHEN** a handler reports that the user cancelled
- **THEN** nothing is loaded and the timer keeps its elapsed time

#### Scenario: Cancelled after writing
- **WHEN** a handler reports a cancellation but made one or more writes
- **THEN** the host behaves as for a failure after writes

### Requirement: A failing handler falls back only if it wrote nothing
When a handler reports failure or throws, and made no writes, the host SHALL run the original load. The fallback SHALL always be visible: a log entry and a non-modal notice naming the plugin.

#### Scenario: Failure before writing
- **WHEN** a handler fails or throws before making any write
- **THEN** the original load runs, the failure is logged and a visible notice says the plugin failed and the standard load was used

### Requirement: A failure after writes does not fall back
When a handler reports failure or throws after having made one or more writes, the host SHALL NOT run the original load, SHALL NOT reset the timer and SHALL show a visible notice stating how many writes were made before the failure.

#### Scenario: Partial failure
- **WHEN** a handler fails after some of its writes succeeded
- **THEN** the original load does not run, the timer is not reset and a notice reports the writes already made

### Requirement: Declining after writing is a contract violation
When a handler declines after having made one or more writes, the host SHALL treat it as a failure with writes.

#### Scenario: Declined with writes
- **WHEN** a handler answers that the load is not for it but made at least one write
- **THEN** the host behaves as for a failure after writes

### Requirement: Handlers are consulted in a fixed order
When more than one plugin has a replacement handler, the host SHALL consult them in order of plugin id, compared ordinally ignoring case, and SHALL stop at the first one that does not decline. A failure from that handler SHALL NOT pass the load to the next handler.

#### Scenario: First non-declining handler wins
- **WHEN** plugins `Alpha` and `beta` both have handlers and `Alpha` handles the load
- **THEN** `beta` is not consulted

#### Scenario: First declines
- **WHEN** `Alpha` declines and `beta` handles the load
- **THEN** `beta`'s result is applied

### Requirement: A plugin is not consulted for its own loads
The host SHALL NOT consult a plugin's replacement handler for a load whose source is that same plugin. Other plugins SHALL still be consulted.

#### Scenario: Own load
- **WHEN** plugin `Alpha` starts a load through the host's time loader
- **THEN** `Alpha`'s handler is not consulted and the other plugins' handlers are

### Requirement: Handlers are never timed out
The host SHALL wait for a handler for as long as it takes, since it may be waiting for a person. Only exceptions are treated as failures.

#### Scenario: Slow handler
- **WHEN** a handler takes several minutes because it is waiting for the user
- **THEN** the host keeps waiting and applies the result when it arrives

### Requirement: Observers are notified of the outcome
After every load attempt that reached the pipeline, the host SHALL notify the observers with what was loaded, who handled it (the host or a plugin id), whether it succeeded, the reason, and the number of writes made. Observers SHALL NOT be able to veto or modify the load. An observer that throws SHALL be logged and SHALL NOT affect the load or the other observers.

#### Scenario: Load handled by the host
- **WHEN** the original load completes
- **THEN** observers are told it was handled by the host, with its outcome

#### Scenario: Load handled by a plugin
- **WHEN** a plugin handles the load
- **THEN** observers are told it was handled by that plugin's id

#### Scenario: Observer throws
- **WHEN** one observer throws
- **THEN** the exception is logged, the other observers still run and the load result is unaffected

#### Scenario: Cancelled load
- **WHEN** a handler reports the user cancelled
- **THEN** observers are notified with a cancelled outcome and no time loaded

### Requirement: A load started from inside a handler does not consult handlers
When a plugin starts a load through the host's time loader while one of its replacement handlers is running, the host SHALL run the original load and notify the observers without consulting any replacement handler, so that loads cannot call each other in a cycle.

#### Scenario: Nested load
- **WHEN** a handler loads time through the host's time loader
- **THEN** no replacement handler is consulted for that nested load and the original load runs

### Requirement: One load per issue at a time
While a load for an issue is in progress, the host SHALL ignore a new request to load time for the same issue.

#### Scenario: Second request while a plugin dialog is open
- **WHEN** a load for an issue is waiting for a plugin and the user asks to load time for that issue again
- **THEN** the second request is ignored and the first continues
