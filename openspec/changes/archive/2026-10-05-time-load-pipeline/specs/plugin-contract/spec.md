# Spec Delta

## MODIFIED Requirements

### Requirement: Minimal Jira facade without exposing credentials
The host SHALL expose a Jira facade limited to: reading an issue's summary, reading time tracking, adding a worklog (optionally with an estimate update method and value), adding a comment, listing an issue's subtasks and creating a subtask. The API token and credentials SHALL NOT be reachable by plugins through the contract.

#### Scenario: Plugin adds a worklog
- **WHEN** a plugin calls the facade to add a worklog to an issue
- **THEN** the host performs the request with its own configured credentials and returns the result without revealing them

#### Scenario: Plugin lists subtasks
- **WHEN** a plugin asks for the subtasks of an issue
- **THEN** it receives public descriptions (key and summary) and no host-internal types

#### Scenario: Plugin creates a subtask
- **WHEN** a plugin asks to create a subtask under an issue
- **THEN** the host creates it with its own credentials and returns the new subtask's key, or a failure without throwing

## ADDED Requirements

### Requirement: Contract 1.1 is additive
The contract version SHALL be 1.1. Everything added in 1.1 SHALL be additive, so a plugin built against 1.0 keeps loading on a 1.1 host, while a plugin declaring 1.1 SHALL NOT load on a 1.0 host.

#### Scenario: Plugin built against 1.0
- **WHEN** a 1.1 host loads a plugin declaring contract 1.0
- **THEN** the plugin loads and works unchanged

#### Scenario: Plugin built against 1.1 on an older host
- **WHEN** a 1.0 host finds a plugin declaring contract 1.1
- **THEN** the plugin is reported as not loaded because its minor is newer than the host's
