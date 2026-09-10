## Purpose

Governs when a row's "open issue in browser" action is available, so the user can only trigger it once the row's key has actually been confirmed against Jira, and the availability tracks the current key rather than a stale one.

## ADDED Requirements

### Requirement: Open action requires a Jira-confirmed issue
The system SHALL make a row's "open in browser" action available only when that row has a non-empty summary resolved from Jira for its current issue key. A non-empty key alone SHALL NOT be sufficient.

#### Scenario: Key typed but not yet resolved
- **WHEN** the user types or pastes an issue key into a row and no summary has been resolved for it yet
- **THEN** the row's "open in browser" action is unavailable

#### Scenario: Key resolves to an existing issue
- **WHEN** the row's issue key resolves to a summary returned by Jira
- **THEN** the row's "open in browser" action becomes available

#### Scenario: Key does not exist in Jira
- **WHEN** the user enters an issue key that Jira reports as not found
- **THEN** the row's "open in browser" action remains unavailable

### Requirement: Open action availability tracks the current key
The system SHALL update a row's "open in browser" availability immediately whenever the row's issue key or resolved summary changes, without requiring an unrelated action to force a refresh.

#### Scenario: Changing an already-resolved key
- **WHEN** a row already has a resolved summary and its "open in browser" action available, and the user changes the issue key to a different value
- **THEN** the row's "open in browser" action becomes unavailable immediately, before the new key's summary has resolved

#### Scenario: New key resolves after being changed
- **WHEN** a row's issue key was just changed and then resolves to a new summary from Jira
- **THEN** the row's "open in browser" action becomes available for the new key
