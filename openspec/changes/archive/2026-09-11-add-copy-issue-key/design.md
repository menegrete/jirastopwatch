## Context

Ver proposal.md - Why. Dos puntos técnicos relevantes hoy:

- `JiraClient.GetIssueSummary` (source/StopWatch/Jira/JiraClient.cs) ya lee
  `issue.Parent.Key` del DTO `ParentFields`, pero solo lo usa para componer
  el string del summary (`"{parent summary} / {issue summary}"`); el key en
  sí se descarta antes de devolver el `string` al llamador.
- `IssueViewModel` (source/StopWatch/Model/IssueViewModel.cs) no tiene hoy
  ninguna propiedad de parent; solo `IssueKey` y `Summary` (el string ya
  compuesto).
- El row template de `MainWindow.xaml` está duplicado en dos
  `DataTemplate` (`IssueRowCompact` y `IssueRowSpacious`) que comparten
  estructura de columnas pero difieren en tamaños.

## Goals / Non-Goals

**Goals:**
- Propagar el parent key como dato estructurado, no como parte del string
  del summary.
- Agregar la interacción de copia sin alterar el layout de columnas fijo de
  la fila (ver proposal - Opción B "ícono on-hover superpuesto", ya
  decidida con el usuario).

**Non-Goals:**
- No se rediseña cómo se compone el texto del summary (`parent-issue-summary`
  sigue componiendo `"{parent summary} / {issue summary}"` igual que hoy).
- No se toca `MiniTimerWindow` (fuera de alcance, ver proposal - Impact).
- No se agrega un mecanismo genérico de "copiar cualquier campo"; son dos
  acciones puntuales (key propio, parent key).

## Decisions

### D1: Cambiar el contrato de `GetIssueSummary`/`GetSummaryAsync` a un tipo estructurado

En vez de que `JiraClient.GetIssueSummary` devuelva un `string`, devuelve un
objeto con el summary compuesto y el parent key (ej. `IssueSummaryResult {
string Summary, string ParentKey }`). `IssueJiraService.GetSummaryAsync`
propaga ese mismo objeto en vez de un `string`.

Alternativa considerada: agregar un método separado
`GetParentKeyAsync(key)` que la UI llame aparte. Se descarta porque
duplicaría el round-trip a Jira que `GetIssueSummary` ya hace (mismo
`Issue.Fields.Parent`), y porque ambos datos (summary y parent key) se
resuelven siempre juntos en el mismo ciclo de refresco del issue.

### D2: `IssueViewModel` expone `ParentKey` como propiedad independiente

Nueva propiedad `string ParentKey` con `INotifyPropertyChanged`, vacía
cuando no hay parent (mismo patrón que `IssueKey`/`Summary`, que nunca son
`null`). Se limpia junto con `Summary` cuando cambia `IssueKey` (mismo
razonamiento ya documentado en el setter de `IssueKey`: el summary y el
parent viejos ya no corresponden al key nuevo).

Nueva propiedad derivada `bool HasParent` (`!string.IsNullOrEmpty(ParentKey)`)
para que el binding de visibilidad del ícono en XAML no dependa de un
converter de string vacío.

### D3: Overlay on-hover en vez de columnas nuevas

Cada ícono de copia se superpone (mismo `Grid.Column` que el control que
acompaña) en vez de sumar una columna a la fila, y su `Visibility` se ata a
un `DataTrigger` sobre un nuevo estado de "mouse sobre la fila", igual al
patrón ya usado para `IsCurrent`/`Accent`. Esto evita repetir la lógica dos
veces al vivir la fila en dos `DataTemplate` (compact/spacious): el
`DataTrigger` de hover es el mismo binding en ambos, solo cambian medidas.

Alternativa considerada: `InlineUIContainer` dentro de un `TextBlock` con
`Inlines`. Se descarta porque el binding hoy es `Text="{Binding Summary}"`
(string plano) y pasar a contenido enriquecido complica el `DataTemplate`
sin necesidad, cuando superponer un control en la misma celda del `Grid`
logra el mismo resultado visual.

- Ícono de copiar el key propio: superpuesto sobre el `TextBox` del
  `IssueKey` (columna del key), pegado al borde derecho.
- Ícono de copiar el parent key: superpuesto en la misma columna del key,
  inmediatamente a la izquierda del ícono anterior; su visibilidad además
  requiere `HasParent` (no solo hover), ya que sin parent no hay nada que
  copiar. Se descartó superponerlo al inicio del summary (decisión
  original) para mantener los dos íconos de copia juntos en un solo lugar
  de la fila.

### D4: Feedback de copiado con estado local transitorio en el ícono

El click en un ícono copia el texto (`Clipboard.SetText`) y dispara un
cambio temporal de su propio glifo a un check por ~1 segundo, con un
`DispatcherTimer` (patrón ya usado en el proyecto para temporizado en UI),
sin pasar por el `ViewModel` — es un estado puramente visual del control,
no un dato persistido.

## Risks / Trade-offs

- [Descubribilidad] Un ícono que solo aparece on-hover puede no ser
  descubierto por el usuario la primera vez → Mitigado con `ToolTip` en
  cada ícono ("Copy issue key" / "Copy parent key"), igual que el resto de
  los botones de la fila.
- [Cambio de contrato] Cambiar el tipo de retorno de `GetIssueSummary` /
  `GetSummaryAsync` toca tests existentes (`JiraClientTest.cs`,
  `IssueViewModelTest.cs`) → Se actualizan como parte de este change
  (ver proposal - Impact); no hay otro llamador de esos métodos fuera del
  flujo de refresco del issue.
- [Regresión de spec] El requirement "Sin interacción sobre el parent" de
  `parent-issue-summary` prohibía cualquier control sobre el parent →
  Resuelto explícitamente en la delta spec de esa capability, acotando la
  excepción al ícono de copia y dejando el resto de la prohibición intacta.
