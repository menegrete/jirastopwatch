## Context

`JiraClient` habla con Jira a través de `IJiraApiRequester` /
`IJiraApiRequestFactory` (RestSharp, API `/rest/api/2`). Hoy el requester
colapsa cualquier error HTTP en `RequestDeniedException` sin status ni cuerpo
(y trata 400 igual que 401), y `JiraClient` traga las excepciones y devuelve
`bool`/`null`. `GetIssueSummary` devuelve un summary compuesto, no apto para
comparar. Motivación y alcance: ver `proposal.md`.

## Goals / Non-Goals

**Goals:**
- Búsqueda JQL, subtasks con summary crudo, tipos de subtask y creación de subtasks.
- Escrituras con resultado tipado (id creado, razón de fallo).
- Sin cambio observable para el usuario final.

**Non-Goals:**
- Soporte de Jira Server/Data Center para búsqueda.
- Metadata de creación (`createmeta`) o campos personalizados al crear.
- Edición/borrado de worklogs; cualquier cosa del sistema de plugins o de #37.

## Decisions

1. **`GetSubtasks` sobre JQL (`parent = KEY`)**, reutilizando `SearchIssues` y
   su paginación, en lugar de leer `fields.subtasks`. Costo: consistencia
   eventual del índice de búsqueda; se documenta en `IJiraOperations` que quien
   crea una subtask debe conservar la key devuelta y no depender de re-buscarla.

2. **Firma tipada para escrituras**: `PostWorklog` y `PostComment` cambian de
   `bool` a un resultado (`Success`, `Id`, `Reason`, `Message`). Se adaptan
   `IssueJiraService`, `PluginJiraApi` y los mocks de `IssueJiraServiceTest`;
   no se dejan métodos `bool` paralelos. `IssueJiraService` conserva su propio
   `PostWorklogResult` y sus reglas.

3. **Solo Jira Cloud**: la búsqueda usa `GET /rest/api/2/search/jql` con
   `fields` explícitos y paginación por `nextPageToken` (sin `total`). Sin
   fallback al `/search` clásico.

4. **Errores de creación reactivos**: `CreateSubtask` envía solo campos
   estándar y, ante un 400, un parser puro convierte `errorMessages` +
   `errors{campo: mensaje}` en un mensaje legible. No se consulta `createmeta`
   (un request extra, forma cambiante en Cloud, y fuera de alcance).
   `GetSubtaskTypes` usa `GET /rest/api/2/project/{key}` (`issueTypes[]` con
   flag `subtask`): sin paginación y sin el `createmeta` deprecado.

5. **Excepción con datos de la respuesta**: `RequestDeniedException` se
   extiende con `StatusCode` (0 si no hubo respuesta) y `ResponseContent`,
   manteniendo sus constructores actuales. RestSharp no lanza por HTTP 4xx/5xx,
   así que status y cuerpo son propiedades; solo los fallos de red tienen una
   excepción original (`response.ErrorException`), que va en `InnerException`.
   Los `catch (RequestDeniedException)` existentes no cambian. Un helper
   privado de `JiraClient` mapea la excepción a la razón de fallo:
   401→`Unauthorized`, 403→`Forbidden`, 404→`NotFound`, 400→`Validation`,
   sin respuesta→`Network`, resto→`Unknown`; `UsernameAndApiTokenNotSet` →
   `Unauthorized`. `ErrorMessage` del requester se alimenta del mensaje de la
   excepción para no romper a quien lo lee.

6. **DTOs**: se extienden `IssueFields`/`IssueTypeFields`/`ProjectFields` con
   key de proyecto, id y nombre del tipo y estado; nuevos DTOs para la página
   de búsqueda, el proyecto con `issueTypes` y el worklog creado (`id`).

## Risks / Trade-offs

- [Cambio del requester afecta a todos los métodos] → los tipos de excepción
  no cambian; el test de `JiraApiRequesterTest` verifica además el `StatusCode`.
- [Búsqueda eventualmente consistente tras crear] → documentado; el consumidor
  cachea la key creada.
- [Cambio de firma rompe mocks existentes] → ajuste mínimo y mecánico en
  `IssueJiraServiceTest`; sus aserciones de comportamiento no cambian.
- [`TreatWarningsAsErrors`] → DTOs nuevos sin warnings de nullabilidad ni usings sobrantes.

## Migration Plan

Cambio interno, sin datos persistidos ni configuración nueva. Se implementa y
se prueba en una sola rama; no requiere rollback específico.

## Open Questions

Ninguna.
