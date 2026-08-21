## MODIFIED Requirements

### Requirement: Sin parent, el summary se muestra sin cambios

Cuando el issue no es un subtask —independientemente de si Jira incluye
`fields.parent` en la respuesta, como ocurre cuando ese campo referencia el
Epic del issue en proyectos team-managed—, o cuando siendo subtask Jira no
incluye información de su parent, el summary mostrado SHALL ser exactamente
el comportamiento actual: solo el summary del issue (con el prefijo de
proyecto si esa opción está activada).

#### Scenario: Issue no es un subtask

- **WHEN** el usuario carga un issue que no tiene parent
- **THEN** el summary mostrado es solo el summary del issue, sin ningún
  agregado

#### Scenario: Issue no es un subtask pero Jira incluye un parent (Epic)

- **WHEN** el usuario carga un issue que no es subtask (por ejemplo, un
  story o task) y la respuesta de Jira incluye `fields.parent` apuntando al
  Epic del issue
- **THEN** el summary mostrado es solo el summary del issue, sin anteponer
  el summary del Epic

#### Scenario: Parent presente pero sin summary

- **WHEN** la respuesta de Jira incluye un parent pero su summary viene vacío
  o ausente
- **THEN** el summary mostrado es solo el summary del issue, como si no
  hubiera parent
