# Proposal

## Why

Hoy quitar una fila (botón de quitar, Ctrl+Delete o Delete en el campo del key) la elimina de inmediato y sin aviso. Si la fila tiene tiempo acumulado que todavía no se cargó a Jira, ese tiempo se pierde en silencio. Issue #55.

## What Changes

- Al quitar una fila que tiene tiempo pendiente de cargar, la aplicación pide confirmación antes de eliminarla, con una ventana propia que respeta el tema activo.
- "Tiempo pendiente de cargar" es exactamente la condición que habilita el botón de cargar tiempo (`IssueViewModel.CanPost`), de modo que el aviso y el botón nunca discrepan.
- Una fila sin tiempo pendiente se sigue quitando de inmediato, sin diálogo.
- La confirmación vive en la capa de UI; `IssueListViewModel.Remove` no pregunta nada, por lo que el resto de los llamadores y los tests no cambian.
- La acción por defecto de la ventana es cancelar (Enter y Escape no eliminan).

## Capabilities

### New Capabilities
- `issue-removal`: cómo se quita una fila de la lista y cuándo se pide confirmación para no perder tiempo sin cargar.

### Modified Capabilities

## Impact

- `source/StopWatch/UI/MainWindow.xaml.cs`: `RemoveIssue` consulta `CanPost` y muestra la ventana de confirmación.
- Nueva ventana WPF `UI/ConfirmRemoveWindow.xaml(.cs)`, con el patrón de `EditTimeWindow` (tema, barra de título oscura, `CenterOwner`).
- Sin cambios en `Model/`, `Settings` ni dependencias. Se publica como `feat:`.
- Fuera de alcance: avisar al limpiar el key o al hacer Reset de una fila (posible issue de seguimiento).
