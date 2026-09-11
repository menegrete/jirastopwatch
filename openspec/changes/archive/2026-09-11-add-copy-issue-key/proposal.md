## Why

Copiar el número de un issue (o el de su padre, cuando el issue es una
subtarea) requiere hoy seleccionar texto a mano dentro del campo del key o
del summary compuesto. El key del padre ni siquiera es seleccionable por sí
solo: vive concatenado dentro del texto del summary
(`"{parent summary} / {issue summary}"`). Copiar rápido cualquiera de los
dos números es una acción frecuente (para pegarlos en un commit, un chat o
una búsqueda de Jira) y hoy no tiene un camino directo.

## What Changes

- Agregar un ícono de copiar, visible solo al pasar el mouse sobre la fila,
  superpuesto sobre el `TextBox` del issue key: copia el key propio al
  portapapeles.
- Agregar un segundo ícono de copiar, visible solo on-hover y solo cuando el
  issue es una subtarea con parent key disponible, superpuesto al inicio del
  texto del summary: copia el key del padre al portapapeles.
- Al hacer click en cualquiera de los dos íconos, el ícono cambia
  brevemente (~1 segundo) a un check de confirmación y vuelve a su estado
  normal.
- Propagar el `ParentKey` del issue como dato estructurado desde
  `JiraClient` hasta `IssueViewModel`, en vez de perderlo una vez compuesto
  el string del summary.
- **Alcance**: solo `MainWindow` (densidades compact y spacious). El
  `MiniTimerWindow` queda fuera de este change.

## Capabilities

### New Capabilities
- `copy-issue-key`: acción de copiar al portapapeles el key propio de un
  issue o el key de su parent (cuando el issue es subtarea), disparada
  desde íconos on-hover en la fila del issue.

### Modified Capabilities
- `parent-issue-summary`: el requirement "Sin interacción sobre el parent"
  prohibía hoy cualquier control o acción asociada al parent dentro del
  summary. Pasa a permitir específicamente el ícono de copia del parent key
  descrito arriba, sin abrir la puerta a otras interacciones (como abrir el
  parent en el navegador o seleccionarlo).

## Impact

- `source/StopWatch/Jira/JiraClient.cs`: `GetIssueSummary` deja de devolver
  solo un `string`; necesita exponer también el parent key junto al summary
  compuesto.
- `source/StopWatch/Model/IssueJiraService.cs`: `GetSummaryAsync` propaga el
  nuevo dato estructurado en vez de un string plano.
- `source/StopWatch/Model/IssueViewModel.cs`: nueva propiedad `ParentKey`
  (o equivalente) con notificación de cambios.
- `source/StopWatch/UI/MainWindow.xaml` y `MainWindow.xaml.cs`: nuevos
  íconos de copia on-hover en ambos `DataTemplate` (compact y spacious),
  manejo de click para copiar al portapapeles y el feedback visual del
  check temporal.
- `openspec/specs/parent-issue-summary/spec.md`: actualizar el requirement
  "Sin interacción sobre el parent".
- Tests existentes de `IssueViewModelTest.cs` y `JiraClientTest.cs` deberán
  cubrir el nuevo dato propagado.
