## 1. DTO

- [x] 1.1 Agregar `IssueTypeFields` (con `Subtask: bool`) en
      `source/StopWatch/Jira/DTO/IssueFields.cs`
- [x] 1.2 Agregar propiedad `IssueType` (tipo `IssueTypeFields`, nullable) a
      `IssueFields`

## 2. Gate de la composición del summary

- [x] 2.1 En `JiraClient.GetIssueSummary` (`source/StopWatch/Jira/JiraClient.cs`),
      anteponer el summary del parent sólo cuando
      `issue.IssueType?.Subtask == true` (además de que
      `issue.Parent?.Fields?.Summary` no sea nulo ni vacío)
- [x] 2.2 Confirmar que un issue con `Parent` poblado pero `IssueType.Subtask`
      en `false` (o `IssueType` ausente) muestra solo su propio summary

## 3. Tests

- [x] 3.1 En `source/StopWatchTest/JiraClientTest.cs`, agregar test: issue no
      subtask con `Parent` poblado (caso Epic en team-managed) → resultado
      es sólo el summary del issue, sin el prefijo del Epic
- [x] 3.2 Confirmar que el test existente de subtask con parent
      (`GetIssueSummary_WithParent_It_Returns_Parent_And_Issue_Summary`)
      sigue pasando una vez que ese fixture incluya `IssueType.Subtask = true`
- [x] 3.3 Confirmar que los tests existentes de "sin parent" y "parent sin
      summary" siguen pasando sin cambios

## 4. Verificación manual

- [x] 4.1 Correr la app contra una instancia real de Jira, seleccionar un
      issue regular (story/task) vinculado a un Epic y confirmar que el
      summary muestra solo el summary del issue, sin el Epic
- [x] 4.2 Confirmar que un subtask genuino sigue mostrando
      `"{parent summary} / {issue summary}"` sin cambios
