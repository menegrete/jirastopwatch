## Purpose

Define cómo se compone el texto de summary mostrado para un issue que es
subtask, agregando el summary del parent, sin requerir ninguna acción ni
configuración adicional del usuario.

## ADDED Requirements

### Requirement: Summary de un subtask incluye el summary del parent

Cuando el issue seleccionado es un subtask y Jira devuelve información de su
parent, el texto de summary mostrado SHALL tener el formato
`"{parent summary} / {issue summary}"`.

#### Scenario: Issue es un subtask con parent disponible

- **WHEN** el usuario carga un issue que es subtask y la respuesta de Jira
  incluye el summary de su parent
- **THEN** el summary mostrado es `"{parent summary} / {issue summary}"`

#### Scenario: Composición con nombre de proyecto activado

- **WHEN** el usuario carga un issue que es subtask con parent disponible
- **AND** la opción de incluir nombre de proyecto está activada
- **THEN** el summary mostrado es `"{project name}: {parent summary} / {issue summary}"`

### Requirement: Sin parent, el summary se muestra sin cambios

Cuando el issue no es un subtask, o Jira no incluye información de parent en
la respuesta, el summary mostrado SHALL ser exactamente el comportamiento
actual: solo el summary del issue (con el prefijo de proyecto si esa opción
está activada).

#### Scenario: Issue no es un subtask

- **WHEN** el usuario carga un issue que no tiene parent
- **THEN** el summary mostrado es solo el summary del issue, sin ningún
  agregado

#### Scenario: Parent presente pero sin summary

- **WHEN** la respuesta de Jira incluye un parent pero su summary viene vacío
  o ausente
- **THEN** el summary mostrado es solo el summary del issue, como si no
  hubiera parent

### Requirement: Falla de la request no afecta el fallback

Cuando la request para obtener el summary falla, el comportamiento SHALL ser
el mismo que existe hoy sin esta funcionalidad, sin mostrar información de
parent.

#### Scenario: La request de summary falla

- **WHEN** la request a Jira para obtener el summary del issue falla
- **THEN** el summary mostrado sigue el comportamiento actual de manejo de
  errores, sin intentar mostrar información de parent

### Requirement: Sin interacción sobre el parent

La información del parent SHALL mostrarse como texto plano dentro del summary
existente, sin agregar ningún control, tooltip o acción (como abrir el parent
en el navegador o seleccionarlo).

#### Scenario: Usuario interactúa con el summary de un subtask

- **WHEN** el usuario hace click o hover sobre el summary de un issue que es
  subtask
- **THEN** no ocurre ninguna acción relacionada al parent (no se abre el
  parent, no aparece un tooltip distinto al que ya existe para el summary)
