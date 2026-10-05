## Purpose

Define cómo el cliente de Jira busca issues por JQL y lista las subtasks de un padre, con el summary tal como está en Jira para poder compararlo.

## Requirements

### Requirement: Búsqueda de issues por JQL

El cliente de Jira SHALL permitir buscar issues mediante una consulta JQL y
devolver, por cada issue, su key, summary crudo, tipo, key del padre (si
existe), proyecto y estado. La búsqueda SHALL recorrer todas las páginas de
resultados que Jira ofrezca hasta agotarlas.

#### Scenario: Búsqueda con resultados en una página

- **WHEN** se busca con un JQL que coincide con issues y Jira los devuelve en una sola página
- **THEN** se devuelve un issue por cada coincidencia con key, summary crudo, tipo, padre, proyecto y estado

#### Scenario: Búsqueda con varias páginas

- **WHEN** Jira indica que hay más resultados mediante un token de página siguiente
- **THEN** se piden las páginas restantes y se devuelve la unión de todas, sin duplicados ni omisiones

#### Scenario: Búsqueda sin resultados

- **WHEN** el JQL no coincide con ningún issue
- **THEN** se devuelve una lista vacía y no se informa error

#### Scenario: Búsqueda sin permiso o sin sesión

- **WHEN** Jira rechaza la búsqueda por credenciales o permisos, o no hay credenciales configuradas
- **THEN** la operación informa el fallo con su razón en lugar de devolver una lista vacía indistinguible de "sin resultados"

### Requirement: Subtasks de un padre con summary crudo

El cliente de Jira SHALL permitir listar las subtasks de un issue padre,
devolviendo para cada una su summary exactamente como está en Jira, sin
anteponer el summary del padre ni el nombre del proyecto.

#### Scenario: Summary crudo

- **WHEN** se listan las subtasks de un padre cuyo summary es "Padre" y una subtask tiene summary "Desarrollo"
- **THEN** el summary devuelto para esa subtask es exactamente "Desarrollo"

#### Scenario: Padre sin subtasks

- **WHEN** el padre no tiene subtasks
- **THEN** se devuelve una lista vacía

#### Scenario: Consistencia eventual documentada

- **WHEN** un consumidor crea una subtask y consulta de inmediato las subtasks del padre
- **THEN** la subtask recién creada puede no aparecer, y la documentación de la operación SHALL advertirlo e indicar que el consumidor conserve la key devuelta al crear
