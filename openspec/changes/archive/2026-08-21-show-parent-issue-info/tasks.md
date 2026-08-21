## 1. DTO

- [x] 1.1 Agregar `ParentFields` (con `Key` y `Fields.Summary`) en
      `source/StopWatch/Jira/DTO/IssueFields.cs`
- [x] 1.2 Agregar propiedad `Parent` (tipo `ParentFields`, nullable) a
      `IssueFields`

## 2. Composición del summary

- [x] 2.1 En `JiraClient.GetIssueSummary` (`source/StopWatch/Jira/JiraClient.cs`),
      componer el texto como `"{parent.Fields.Summary} / {summary}"` cuando
      `issue.Fields.Parent?.Fields?.Summary` no sea nulo ni vacío
- [x] 2.2 Mantener el comportamiento actual (solo `summary`) cuando no hay
      parent, o cuando el summary del parent es nulo o vacío
- [x] 2.3 Mantener el prefijo de `IncludeProjectName` como el más externo:
      `"{project}: {parent summary} / {summary}"`
- [x] 2.4 Confirmar que el catch de `RequestDeniedException` sigue devolviendo
      `string.Empty` sin cambios

## 3. Tests

- [x] 3.1 En `source/StopWatchTest/JiraClientTest.cs`, agregar test: issue
      subtask con parent con summary → resultado `"{parent} / {issue}"`
- [x] 3.2 Agregar test: issue sin parent → resultado igual al summary actual
      (sin cambios de comportamiento)
- [x] 3.3 Agregar test: parent presente pero con summary vacío/nulo →
      resultado igual al summary actual
- [x] 3.4 Agregar test: subtask con parent + `IncludeProjectName` activo →
      resultado `"{project}: {parent} / {issue}"`

## 4. Verificación manual

- [x] 4.1 Correr la app contra una instancia real de Jira, seleccionar un
      subtask y confirmar que el summary de fila muestra
      `"{parent summary} / {issue summary}"`
- [x] 4.2 Confirmar que un issue que no es subtask sigue mostrando el summary
      sin cambios
- [x] 4.3 Confirmar que no se agregó ninguna interacción nueva (click, hover,
      tooltip) sobre la porción de parent del summary
