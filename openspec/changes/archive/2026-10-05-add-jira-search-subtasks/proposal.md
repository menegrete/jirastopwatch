# Proposal

## Why

El cliente de Jira no puede buscar issues, listar ni crear subtasks, y las
escrituras (`PostWorklog`, `PostComment`) devuelven solo `bool`: se pierde el
motivo del fallo y el id del worklog creado. Un consumidor como el plugin de
importación de tiempos (#37) necesita encontrar o crear la subtask donde
cargar el tiempo, y saber por qué falló una carga. Son mejoras a
`JiraClient` / `IJiraOperations` independientes del sistema de plugins.

## What Changes

- `IJiraOperations.SearchIssues(jql)`: búsqueda por JQL contra el endpoint
  vigente de Jira Cloud (`/rest/api/2/search/jql`), con paginación por
  `nextPageToken`. Devuelve key, summary crudo, tipo, padre, proyecto y estado.
- `IJiraOperations.GetSubtasks(parentKey)`: subtasks de un padre con su
  **summary crudo** (sin composición con el padre ni prefijo de proyecto),
  implementado sobre `SearchIssues` con `parent = KEY`.
- `IJiraOperations.GetSubtaskTypes(projectKey)`: tipos de issue subtask del
  proyecto; lista vacía si no hay ninguno.
- `IJiraOperations.CreateSubtask(parentKey, summary, issueTypeId)`: crea una
  subtask con campos estándar y devuelve la key creada o un error legible.
  Los campos obligatorios adicionales del proyecto fallan con un mensaje
  claro (parseado del cuerpo `errors{}` de Jira), nunca en silencio.
- **BREAKING (interno)**: `PostWorklog` y `PostComment` pasan de `bool` a un
  resultado tipado con éxito, id del worklog creado y, ante error, una razón
  estructurada (`Unauthorized`, `Forbidden`, `NotFound`, `Validation`,
  `Network`, `Unknown`) más el mensaje de Jira. `IssueJiraService` y
  `PluginJiraApi` se adaptan con el mínimo cambio; la regla "solo se resetea
  el timer si todo se posteó" no cambia.
- `RequestDeniedException` pasa a llevar `StatusCode` y `ResponseContent`;
  los fallos de red usan la excepción original como `InnerException`. El
  requester sigue lanzándola igual, así que los `catch` existentes no cambian.
- Sin cambio de comportamiento observable para el usuario final.

## Capabilities

### New Capabilities
- `jira-issue-search`: búsqueda de issues por JQL y listado de subtasks de un
  padre con summary crudo.
- `jira-subtask-creation`: consulta de tipos de subtask de un proyecto y
  creación de subtasks con campos estándar, con errores legibles.
- `jira-write-results`: resultado tipado de las escrituras de worklog y
  comentario, con id creado y razón de fallo estructurada.

### Modified Capabilities
<!-- Ninguna: no cambia el comportamiento visible de capacidades existentes. -->

## Impact

- Código: `Jira/JiraClient.cs`, `Jira/JiraApiRequester.cs`,
  `Jira/JiraApiRequestFactory.cs` (+ interfaces), DTOs en `Jira/DTO/`,
  `Model/IJiraOperations.cs`, `Model/IssueJiraService.cs`,
  `Plugins/PluginHostAdapters.cs`.
- Tests: nuevos en `JiraClientTest` y `JiraApiRequestFactoryTest`; ajustes en
  `JiraApiRequesterTest` e `IssueJiraServiceTest` (mocks de `PostWorklog` /
  `PostComment`).
- Solo Jira Cloud para búsqueda; sin dependencias nuevas.
- Fuera de alcance: sistema de plugins, pipeline de carga (#37), creación de
  issues que no sean subtasks, campos personalizados al crear, edición o
  borrado de worklogs.
- Relacionados: #36 (este change), #35 plugin-host, #37 time-load-pipeline.
