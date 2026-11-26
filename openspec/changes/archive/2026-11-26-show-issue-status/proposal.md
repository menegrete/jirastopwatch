# Proposal

## Why

La lista de issues no muestra en qué estado de Jira está cada issue. Para saber si una issue sigue en To Do, está en curso o ya se cerró, el usuario tiene que abrirla en el navegador. Jira ya devuelve el estado en la misma respuesta que usamos para el summary, así que mostrarlo no agrega llamadas.

## What Changes

- Cada fila de la ventana principal muestra un ícono de estado entre el botón de abrir en el navegador y el summary, en las dos densidades (compact y spacious).
- La vista mini muestra el mismo ícono entre el key y el summary de cada fila.
- El ícono representa la categoría del estado en Jira (To Do, In Progress, Done) con una forma y un color propios de cada categoría: círculo vacío, círculo a medias y círculo lleno con check. Sin color, las tres categorías se siguen distinguiendo por la forma.
- El tooltip del ícono muestra el nombre del estado tal como lo define Jira (por ejemplo, "Code Review").
- Un estado con categoría desconocida se presenta como To Do. Una fila sin key, o sin estado resuelto, no muestra ícono.
- El estado se actualiza junto con el summary, y también justo después de que la app transiciona la issue al iniciar un timer.
- No incluye cambiar el estado desde el ícono, ni mostrarlo en el widget de la barra de tareas.

## Capabilities

### New Capabilities
- `issue-status`: cómo se presenta el estado de Jira de cada issue en la ventana principal y en la vista mini, y cuándo se actualiza.

### Modified Capabilities

## Impact

- `source/StopWatch/Jira/DTO/IssueFields.cs`, `Jira/JiraClient.cs`: leer `status.name` y `status.statusCategory.key` de la respuesta que ya se pide.
- `source/StopWatch/Model/IssueJiraService.cs`, `IssueViewModel.cs`, `MiniTimerRowViewModel.cs`: llevar el nombre y la categoría del estado hasta las filas.
- `source/StopWatch/UI/MainWindow.xaml(.cs)`, `MiniTimerWindow.xaml`, `Styles.xaml`: columna nueva con el ícono y refresco tras la transición automática.
- `source/StopWatch/UI/Theme.cs`, `ThemeBrushes.cs`: tres colores nuevos, uno por categoría, en ambos temas.
- Sin llamadas nuevas a Jira ni dependencias nuevas. Se publica como `feat:`.
