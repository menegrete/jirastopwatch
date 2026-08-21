## Why

El cambio `show-parent-issue-info` compone el summary con el parent cuando
`fields.parent` está presente en la respuesta de Jira. Eso asumía que
`fields.parent` sólo aparece en subtasks, pero en proyectos team-managed
Jira también lo popula para issues regulares (story, task, bug) que están
vinculados a un Epic: ahí `fields.parent` es el Epic, no un parent de
subtask. Como resultado, un issue que no es subtask muestra hoy
`"{Epic summary} / {issue summary}"`, lo cual es incorrecto: el Epic no es
el "padre" en el sentido que la feature quería capturar (jerarquía
subtask → parent task), y el requirement "Sin parent, el summary se
muestra sin cambios" del spec `parent-issue-summary` está siendo violado
para estos issues.

## What Changes

- **La composición del summary con parent sólo aplica cuando el issue es un
  subtask.** Se agrega el chequeo de `fields.issuetype.subtask == true`
  (campo que Jira siempre expone, sea el proyecto company-managed o
  team-managed) antes de anteponer el summary del parent.
- **`IssueFields` gana un campo `IssueType`** (con `Subtask: bool`), leído
  del mismo fetch de summary que ya trae `fields.issuetype`.
- **Issues regulares vinculados a un Epic vuelven a mostrar sólo su propio
  summary**, igual que cualquier issue sin parent.
- **Sin cambios de API ni de request**: se lee un campo adicional
  (`fields.issuetype.subtask`) que el fetch existente ya trae.

### Non-goals

- No se cambia el formato `parent / issue` para subtasks genuinos; sigue
  igual que en `show-parent-issue-info`.
- No se agrega soporte para mostrar el Epic de un issue regular; eso sería
  una feature distinta y no lo que este bug pide.

## Capabilities

### Modified Capabilities

- `parent-issue-summary`: el requirement "Sin parent, el summary se muestra
  sin cambios" pasa a cubrir explícitamente el caso "el issue no es un
  subtask, aunque `fields.parent` esté presente" (por ejemplo, un Epic
  link en un proyecto team-managed).

## Impact

**Código afectado**

- `source/StopWatch/Jira/DTO/IssueFields.cs` — nueva clase `IssueTypeFields`
  (`Subtask: bool`) y propiedad `IssueType` en `IssueFields`.
- `source/StopWatch/Jira/JiraClient.cs` — `GetIssueSummary` sólo antepone el
  summary del parent cuando `issue.IssueType?.Subtask == true`.
- `source/StopWatchTest/JiraClientTest.cs` — nuevo test: issue no-subtask
  con `Parent` poblado (caso Epic) → resultado es sólo el summary del
  issue, sin el prefijo.

**Dependencias**

Ninguna nueva. Se lee `fields.issuetype.subtask`, que la Jira REST API ya
incluye en la respuesta sin filtro de campos usada por `GetIssueSummary`.

**Riesgo**

Bajo. Es un chequeo adicional sobre datos ya disponibles; no cambia el
comportamiento para subtasks genuinos ni para issues sin ningún parent.
