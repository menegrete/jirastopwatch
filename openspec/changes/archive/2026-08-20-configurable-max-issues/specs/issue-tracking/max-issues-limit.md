## Purpose

Allows users to configure the maximum number of issues they can track simultaneously, enabling flexibility for different workflow needs while maintaining reasonable bounds.

## ADDED Requirements

### Requirement: User can set maximum issues limit

The system SHALL allow users to configure the maximum number of issues they can display and track. The limit SHALL be persisted per user in their application settings file and SHALL apply immediately upon closing the settings dialog.

#### Scenario: User increases limit from default
- **WHEN** user opens Settings dialog and changes the max issues value to 30
- **THEN** the new limit of 30 is saved to settings file
- **AND** the "Add Issue" button becomes enabled up to 30 issues on the current session

#### Scenario: User decreases limit below current issue count
- **WHEN** user has 25 issues loaded and changes the max limit to 15
- **THEN** the setting is saved to 15
- **AND** existing issues above the new limit remain displayed (no forced removal)
- **AND** attempting to add a new issue is prevented with tooltip "You have reached the max limit of 15 issues and cannot add another"

#### Scenario: Settings are persisted across application restart
- **WHEN** user sets max issues to 25, closes the application, and reopens it
- **THEN** the max issues setting is 25 (not reset to default 20)

### Requirement: Maximum issues limit constraints

The system SHALL enforce boundary constraints on the maximum issues limit: minimum of 1 issue, maximum of 40 issues.

#### Scenario: User attempts to set invalid limit too low
- **WHEN** user tries to set max issues to 0 or negative number
- **THEN** the input field rejects the value and does not save
- **AND** the current valid value is retained

#### Scenario: User attempts to set invalid limit too high
- **WHEN** user tries to set max issues to 41 or higher
- **THEN** the input field rejects the value and does not save
- **AND** the current valid value is retained

### Requirement: Default limit for new users

New installations and existing users without a configured limit SHALL default to 20 issues maximum.

#### Scenario: First-time user gets default limit
- **WHEN** application runs for the first time
- **THEN** the max issues limit defaults to 20
- **AND** users can add up to 20 issue tracking rows

#### Scenario: Upgraded user without prior setting gets default
- **WHEN** application loads a configuration without a MaxIssues setting
- **THEN** the limit defaults to 20
- **AND** the new setting is persisted in the configuration file

### Requirement: Add issue button behavior respects limit

The "Add Issue" button (Ctrl+N) behavior SHALL change dynamically based on the current maximum issues limit setting.

#### Scenario: Add button enabled when below limit
- **WHEN** user has 5 issues configured and max limit is 20
- **THEN** clicking the "Add Issue" button (or pressing Ctrl+N) adds a new issue row

#### Scenario: Add button disabled when at limit
- **WHEN** user has 20 issues configured and max limit is 20
- **THEN** clicking the "Add Issue" button does not add a new issue
- **AND** tooltip displays "You have reached the max limit of 20 issues and cannot add another"
- **AND** button cursor changes to "not allowed" cursor

#### Scenario: Dynamic adjustment after setting change
- **WHEN** user changes max limit from 20 to 30 while the app is running
- **AND** user currently has 25 issues
- **THEN** the "Add Issue" button immediately becomes enabled
- **AND** user can now add up to 5 more issues
