# Spec Delta

## Purpose

Defines how a plugin takes part in time loading: registering a replacement handler and observers, what a handler receives and may answer, and the two distinct ways of posting time (raw and through the host's loader).

## ADDED Requirements

### Requirement: A plugin registers at most one replacement handler
A plugin SHALL be able to register one replacement handler through the host object it receives at initialization. The host SHALL associate the handler with that plugin's id.

#### Scenario: Registering a handler
- **WHEN** a plugin registers a replacement handler during initialization
- **THEN** the host consults it, labelled with the plugin's id, on later time loads

#### Scenario: No handler registered
- **WHEN** a plugin registers no handler
- **THEN** the host never consults it

### Requirement: The handler receives the confirmed load
A replacement handler SHALL receive the issue key, start time, elapsed time, comment, estimate update method and value, the source of the load and a Jira API scoped to that invocation.

#### Scenario: Request content
- **WHEN** the host consults a handler for a load the user confirmed
- **THEN** the request carries the values from the host dialog and the source `user`

### Requirement: The handler answers declined, handled, cancelled or failed
A handler SHALL answer one of: declined (not for it), handled (with the time it loaded), cancelled (the user closed the plugin's dialog) or failed (with an optional reason). An exception thrown by the handler SHALL be treated as a failure.

#### Scenario: Exception
- **WHEN** a handler throws
- **THEN** the host treats it as a failure and logs the exception

### Requirement: The scoped Jira API counts writes
The Jira API passed to a handler SHALL count the writes (worklogs and subtasks) that succeeded during that invocation. The host SHALL use that count to choose between fallback and no fallback. Calls made through it SHALL NOT trigger the pipeline.

#### Scenario: Counting
- **WHEN** a handler adds two worklogs and creates a subtask, all accepted by Jira
- **THEN** the host sees three writes for that invocation

#### Scenario: Failed write
- **WHEN** a write through the scoped API is rejected by Jira
- **THEN** it is not counted

### Requirement: Raw posting does not enter the pipeline
Posting a worklog through the Jira API SHALL be a raw operation: it SHALL NOT run replacement handlers nor notify observers. A worklog post SHALL accept an estimate update method and value, so a plugin that handles a load can respect what the user chose.

#### Scenario: Plugin posts inside its handler
- **WHEN** a handler posts a worklog through the Jira API
- **THEN** no handler is consulted and no observer is notified for that post

#### Scenario: Estimate honored
- **WHEN** a handler posts a worklog with the estimate method and value from the request
- **THEN** Jira receives that estimate update

### Requirement: A plugin can load time through the host
The host SHALL give plugins a time loader that runs a load through the full pipeline with the plugin as its source. Other plugins SHALL be able to react to it; the originating plugin SHALL NOT.

#### Scenario: Load through the loader
- **WHEN** plugin `Alpha` loads time through the host's time loader
- **THEN** the load runs through the pipeline with source `plugin:Alpha`, other plugins' handlers are consulted and observers are notified

#### Scenario: Result returned
- **WHEN** the load completes
- **THEN** the loader returns whether it succeeded and why not if it did not

### Requirement: Plugins can observe loads
A plugin SHALL be able to subscribe to a notification of every load outcome. Notifications SHALL use public contract types only.

#### Scenario: Subscribing
- **WHEN** a plugin subscribes and a load completes
- **THEN** it is notified with the outcome and who handled it
