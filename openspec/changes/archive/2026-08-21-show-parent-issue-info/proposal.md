## Why

Cuando el issue en una fila es un subtask, hoy solo se ve su propio summary
(`GetIssueSummary` en `JiraClient.cs`). El usuario pierde de vista de qué tarea
padre depende ese subtask sin abrir el issue en el navegador. El payload que ya
devuelve Jira para el fetch de summary (`GET /issue/{key}` sin filtro de campos)
incluye `fields.parent` cuando el issue es un subtask, así que la información ya
llega hoy y simplemente no se lee.

## What Changes

- **`IssueFields` gana un campo `Parent` anidado** (key + summary del padre),
  poblado solo cuando Jira lo incluye en la respuesta (i.e. el issue es subtask).
- **`GetIssueSummary` compone el texto como `"{parent.summary} / {summary}"`**
  cuando hay parent; si no hay parent, o si `Parent.Fields.Summary` viene vacío,
  el comportamiento es exactamente el de hoy (solo el summary propio). Con
  `IncludeProjectName` activo, el nombre del proyecto sigue siendo el prefijo
  más externo: `"Project: parent.summary / summary"`.
- **Sin cambios de API ni de request**: se lee un campo que el fetch existente
  de summary ya trae; no hay round-trip nuevo.
- **Sin interacción nueva**: es texto plano en el label de summary existente, no
  hay control, tooltip ni click especial para el parent.
- **Sin setting nuevo**: la composición es siempre así para subtasks, no hay
  toggle en Settings (a diferencia de `IncludeProjectName`).
- **Fallback silencioso**: si la request falla (`RequestDeniedException`) o el
  issue no es un subtask, se comporta igual que hoy.

### Non-goals

- No se resuelve una cadena de parents anidados (subtask de subtask). Solo un
  nivel, que es lo único que Jira expone en `fields.parent`.
- No hay navegación ni acción sobre el parent (abrir en browser, seleccionarlo
  como fila, etc.).
- No se agrega ningún setting para activar/desactivar esto.

## Capabilities

### New Capabilities

- `parent-issue-summary`: cómo se compone el summary mostrado para un issue que
  es subtask, incluyendo el formato `parent / issue`, su interacción con
  `IncludeProjectName`, y el fallback cuando no hay parent o falla el fetch.

### Modified Capabilities

Ninguna. No hay specs existentes que describan el comportamiento actual de
`GetIssueSummary`.

## Impact

**Código afectado**

- `source/StopWatch/Jira/DTO/IssueFields.cs` — nueva clase `ParentFields` (Key,
  Fields.Summary) y propiedad `Parent` en `IssueFields`.
- `source/StopWatch/Jira/JiraClient.cs` — `GetIssueSummary` compone el string
  con el parent cuando está presente.
- `source/StopWatch/UI/IssueControl.cs` — sin cambios de lógica; `UpdateSummary()`
  sigue siendo el único punto que llama a `GetIssueSummary` y asigna
  `lblSummary.Text`, así que el nuevo formato llega gratis.

**Dependencias**

Ninguna nueva. Se lee un campo (`fields.parent`) que la Jira REST API ya incluye
en la respuesta de `GET /issue/{key}` para subtasks.

**Riesgo**

Bajo. Es una composición de string adicional sobre un dato ya disponible, con
fallback al comportamiento actual en cualquier caso de ausencia o error.
