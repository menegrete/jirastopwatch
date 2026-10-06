# Spec Delta

## MODIFIED Requirements

### Requirement: Minimal Jira facade without exposing credentials
The host SHALL expose a Jira facade limited to: reading an issue's summary, reading time tracking, adding a worklog (optionally with an estimate update method and value), adding a comment, listing an issue's subtasks, reading a project's subtask types and creating a subtask (optionally of a named type). The API token and credentials SHALL NOT be reachable by plugins through the contract.

#### Scenario: Plugin adds a worklog
- **WHEN** a plugin calls the facade to add a worklog to an issue
- **THEN** the host performs the request with its own configured credentials and returns the result without revealing them

#### Scenario: Plugin lists subtasks
- **WHEN** a plugin asks for the subtasks of an issue
- **THEN** it receives public descriptions (key, summary and issue type name) and no host-internal types

#### Scenario: Plugin creates a subtask
- **WHEN** a plugin asks to create a subtask under an issue
- **THEN** the host creates it with its own credentials and returns the new subtask's key, or a failure without throwing

### Requirement: Contract 1.1 is additive
Everything added in contract 1.1 SHALL be additive, so a plugin built against 1.0 keeps loading on a host that implements 1.1 or later, while a plugin declaring 1.1 SHALL NOT load on a 1.0 host.

#### Scenario: Plugin built against 1.0
- **WHEN** a host implementing 1.1 or later loads a plugin declaring contract 1.0
- **THEN** the plugin loads and works unchanged

#### Scenario: Plugin built against 1.1 on an older host
- **WHEN** a 1.0 host finds a plugin declaring contract 1.1
- **THEN** the plugin is reported as not loaded because its minor is newer than the host's

## ADDED Requirements

### Requirement: Subtask type can be chosen by name
A plugin SHALL be able to create a subtask of a given issue type by passing the type's name. The host SHALL match the name against the subtask types the parent's project offers, ignoring case and surrounding whitespace, and create the subtask with that type.

#### Scenario: Known type
- **WHEN** a plugin creates a subtask naming a type the project offers
- **THEN** the subtask is created with that type and its key is returned

#### Scenario: Name differs in case
- **WHEN** a plugin names a type with different letter case than the project's
- **THEN** the project's type is used

#### Scenario: Type not offered by the project
- **WHEN** a plugin names a subtask type the project does not offer
- **THEN** nothing is created, the result is a failure, the reason is logged by the host and no other type is used instead

#### Scenario: No type given
- **WHEN** a plugin creates a subtask with a null or empty type name
- **THEN** the behavior is the same as creating a subtask without naming a type: the project's first subtask type is used

### Requirement: Subtasks expose their issue type
The public description of a subtask SHALL carry the name of its issue type, so a plugin can restrict reuse of subtasks to the type it creates. The name SHALL be empty when it is unknown.

#### Scenario: Listed subtask carries its type
- **WHEN** a plugin lists the subtasks of an issue
- **THEN** each description carries the name of that subtask's issue type

#### Scenario: Type unknown
- **WHEN** a subtask description is built without a type
- **THEN** its issue type is an empty string, not null

### Requirement: Plugins can read a project's subtask types
The host SHALL let a plugin read the names of the subtask types a project offers, so it can present a choice in its own settings.

#### Scenario: Project offers subtask types
- **WHEN** a plugin asks for the subtask types of a project
- **THEN** it receives their names

#### Scenario: Project has no subtask types
- **WHEN** the project offers no subtask types
- **THEN** the result is an empty list

#### Scenario: Types cannot be read
- **WHEN** the types cannot be read (no session, request failure or an invalid project key)
- **THEN** the result is a failure without throwing

### Requirement: Subtask creation by type is counted as a write
During a time-load handler, creating a subtask SHALL count as a write whichever overload is used, and only when a subtask was actually created.

#### Scenario: Subtask of a named type created
- **WHEN** a handler creates a subtask naming a type and the host creates it
- **THEN** it counts as one write

#### Scenario: Named type not offered
- **WHEN** a handler names a type the project does not offer
- **THEN** nothing is counted

### Requirement: Contract 1.2 is additive
The contract version SHALL be 1.2. Everything added in 1.2 SHALL be additive, so a plugin built against 1.1 keeps loading on a 1.2 host, while a plugin declaring 1.2 SHALL NOT load on a 1.1 host.

#### Scenario: Plugin built against 1.1
- **WHEN** a 1.2 host loads a plugin declaring contract 1.1
- **THEN** the plugin loads and works unchanged, including its two-argument subtask creation and its subtask descriptions without a type

#### Scenario: Plugin built against 1.2 on an older host
- **WHEN** a 1.1 host finds a plugin declaring contract 1.2
- **THEN** the plugin is reported as not loaded because its minor is newer than the host's
