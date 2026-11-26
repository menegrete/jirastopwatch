# Design

## Context

`JiraClient.GetIssueSummary` pide `GET /rest/api/2/issue/{key}` sin filtro de `fields`, así que la respuesta ya trae `fields.status.name` y `fields.status.statusCategory.key`. `IssueFields.Status` (`StatusFields`) existe pero solo mapea `Name`, y `GetIssueSummary` no lo copia a `IssueSummaryResult`.

`MainWindow` resuelve el summary de cada fila en cada tick de 30 s (`UpdateSummaries`), al validar la sesión y al editar o pegar un key. `IssueJiraService.GetSummaryAsync` devuelve `null` cuando Jira rechaza la request (la fila conserva lo que tenía) y un resultado vacío sin key o sin sesión.

`ChangeIssueStateAsync` aplica las transiciones de inicio (`Settings.StartTransitions`) al iniciar un timer, sin refrescar la fila después.

Las filas tienen dos plantillas (`IssueRowCompact` e `IssueRowSpacious`), ambas con el botón de abrir en la columna 2 (`Auto`) y el summary en la 3 (`*`). La vista mini lleva el key en la columna 0 y el summary en la 2. `MiniTimerRowViewModel` envuelve un `IssueViewModel` y reenvía sus propiedades.

## Goals / Non-Goals

**Goals:**
- Que el estado llegue a las filas sin requests nuevas a Jira.
- Que la categoría quede tipada en el modelo, no como string de Jira, para que la UI y los tests no dependan del texto `new`/`indeterminate`/`done`.

**Non-Goals:**
- Cambiar el estado desde el ícono.
- Mostrar el estado en el widget de la barra de tareas.
- Persistir el estado entre ejecuciones: el summary tampoco se persiste y ambos se resuelven en el mismo momento.

## Decisions

### La categoría viaja como enum desde `JiraClient`

`IssueSummaryResult` suma `StatusName` (string) y `StatusCategory` (enum `IssueStatusCategory { None, ToDo, InProgress, Done }`). `JiraClient` mapea `statusCategory.key`: `new` → `ToDo`, `indeterminate` → `InProgress`, `done` → `Done`, cualquier otro valor con nombre de estado presente → `ToDo`, y sin estado → `None`. `StatusFields` suma `StatusCategory { Key }`.

Alternativa descartada: mapear por `statusCategory.colorName`. Jira puede cambiar los nombres de color. La `key` es el identificador estable de las tres categorías.

### `IssueViewModel` expone `StatusName` y `StatusCategory`

Los dos campos se asignan en `UpdateSummaryAsync`, en el mismo bloque que `Summary` y `ParentKey`, así que el caso `null` (request rechazada) conserva el estado anterior sin código nuevo. Con un resultado vacío, `StatusCategory` queda en `None` y el ícono se oculta. `ITimerSource` suma ambos, y `MiniTimerRowViewModel` los lee de su fuente y los notifica en su `Refresh` de cada segundo, igual que hace con `Summary`. Al cambiar el key, `IssueViewModel` vacía el estado junto con el summary.

### El ícono es un `Path` por categoría en una columna de ancho fijo

Tres geometrías nuevas en `Styles.xaml`, junto a `GlyphOpenBrowser` y las demás: círculo con contorno (To Do), círculo con la mitad derecha rellena (In Progress) y círculo relleno con un check calado (Done). Un estilo `StatusIcon` elige geometría y brush con `DataTrigger` sobre `StatusCategory` y se colapsa con `None`.

La columna nueva es de ancho fijo (12 px más margen), no `Auto`. Así el summary no se mueve cuando el estado llega después que la fila, y todas las filas reservan el mismo espacio. El ícono se inserta en ambas plantillas de `MainWindow` y en la plantilla de fila de `MiniTimerWindow`, que corre una columna sus elementos posteriores.

Alternativa descartada: pill con el nombre del estado. Le quita ancho variable al summary, y un pill vacío solo de color dejaría la categoría comunicada únicamente por color, contra el criterio que ya aplica la barra de selección de la fila.

### Tres colores semánticos nuevos en `Theme`

`StatusToDo`, `StatusInProgress` y `StatusDone`, con valores para los dos temas, registrados en `ThemeBrushes` como los demás para que el `DynamicResource` siga el cambio de tema en ejecución. Punto de partida: gris (`TextMuted`), azul y verde distintos de `SurfaceActive` y `TimerRunning`. Los valores finales se ajustan contra los tres fondos de fila en cada tema (ver Riesgos).

### Refresco tras la transición automática

`ChangeIssueState` pasa a recibir la fila en lugar de solo su key (su único llamador, `issues_TimerStarted`, ya la tiene). Al volver del `Task.Run` en el hilo de UI, llama a `UpdateSummary` si `DoTransition` tuvo éxito y la fila sigue con el mismo key. Es una request extra solo cuando hubo transición.

## Risks / Trade-offs

- [El azul de In Progress se confunde con `SurfaceActive` en una fila seleccionada, o el verde de Done con `TimerRunning`] → la forma ya distingue la categoría; además, los colores se eligen y verifican sobre los tres fondos de fila en ambos temas antes de cerrar la tarea.
- [Un ícono de 12 px es poco legible a escalas de DPI bajas] → las geometrías se dibujan con trazo de al menos 1.5 px y se revisan al 100 % de escala.
- [Workflows con muchos estados distintos en la misma categoría] → el ícono no los distingue a propósito; el tooltip da el nombre exacto.
