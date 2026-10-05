# Spec Delta

## ADDED Requirements

### Requirement: Updates preserve installed plugins
Applying an update SHALL NOT remove, replace or modify installed plugins, regardless of whether the plugins live next to the executable or in the per-user data folder.

#### Scenario: Update with plugins installed
- **WHEN** an update is applied on a machine with plugins installed
- **THEN** the same plugin folders and files exist after the update and load on next start
