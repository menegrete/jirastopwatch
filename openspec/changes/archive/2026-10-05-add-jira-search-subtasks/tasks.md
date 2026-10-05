# Tasks

## 1. Excepción con datos de la respuesta

- [x] 1.1 Extender `RequestDeniedException` con `StatusCode` y `ResponseContent` (conservando los constructores actuales) y lanzarla desde `JiraApiRequester` con status, cuerpo y `ErrorException` como inner; verificar con tests en `JiraApiRequesterTest` (401, 400, 403, 404, error de red) que los tipos lanzados no cambian y que las propiedades se completan
- [x] 1.2 Mantener `ErrorMessage` del requester como hasta ahora (se asigna solo fuera de 401/400, desde el mensaje de RestSharp, que es también el mensaje de la excepción); verificar con tests de `ThrowIfFailed` que sigue poblado tras un error de red y que un 401 lo deja intacto

## 2. Resultados tipados de escritura

- [x] 2.1 Definir la razón de fallo y el resultado de escritura, y un helper en `JiraClient` que mapea excepción→razón y parsea `errorMessages`/`errors{}`; verificar con tests unitarios del parser y del mapeo (incluye `UsernameAndApiTokenNotSetException`)
- [x] 2.2 Cambiar `PostWorklog` y `PostComment` en `IJiraOperations`/`JiraClient` para devolver el resultado tipado, con el id del worklog creado leído de la respuesta; verificar con tests en `JiraClientTest` (éxito con id, y una razón por cada tipo de fallo)
- [x] 2.3 Adaptar `IssueJiraService` y `PluginJiraApi` al nuevo resultado y actualizar los mocks en `IssueJiraServiceTest`; verificar que `dotnet test StopWatch.sln --settings .runsettings` pasa sin cambiar las aserciones de comportamiento

## 3. Búsqueda y subtasks

- [x] 3.1 Extender los DTOs de `Jira/DTO/` y agregar al factory el request de búsqueda (`/rest/api/2/search/jql`, `fields`, `nextPageToken`); verificar con tests en `JiraApiRequestFactoryTest` (URL, query, método)
- [x] 3.2 Implementar `SearchIssues(jql)` con paginación por `nextPageToken`; verificar con tests en `JiraClientTest`: una página, varias páginas, vacío, error de permiso y de red
- [x] 3.3 Implementar `GetSubtasks(parentKey)` sobre `SearchIssues` con `parent = KEY` devolviendo el summary crudo, y documentar la consistencia eventual en `IJiraOperations`; verificar con un test de summary crudo y uno de padre sin subtasks

## 4. Creación de subtasks

- [x] 4.1 Agregar al factory los requests de proyecto (`/rest/api/2/project/{key}`) y de creación de issue, e implementar `GetSubtaskTypes(projectKey)`; verificar con tests de factory y de cliente (varios tipos, sin tipos de subtask)
- [x] 4.2 Implementar `CreateSubtask(parentKey, summary, issueTypeId)` con campos estándar y devolución de la key; verificar con tests en `JiraClientTest`: éxito, campos obligatorios faltantes (mensaje con cada campo), tipo inválido, 403 y 404

## 5. Verificación final

- [x] 5.1 Ejecutar `dotnet build StopWatch.sln` y `dotnet test StopWatch.sln --settings .runsettings` y verificar que ambos pasan con `TreatWarningsAsErrors` activo
