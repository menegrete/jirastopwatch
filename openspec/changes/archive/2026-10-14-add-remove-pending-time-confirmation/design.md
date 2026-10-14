# Design

## Context

Las tres vías para quitar una fila (botón, Ctrl+Delete, Delete en el campo del key) convergen en `MainWindow.RemoveIssue`, que llama a `IssueListViewModel.Remove`. El evento `RemoveRequested`/`RequestRemove` del view model no tiene suscriptores. El botón de cargar tiempo se habilita con `IssueViewModel.CanPost` (`ceil(minutos) >= 1`, es decir, cualquier tiempo > 0). Ver proposal.md - Why.

## Goals / Non-Goals

**Goals:**
- Un único punto de confirmación que cubra todas las vías de quitar.
- Que "pendiente" no pueda divergir del botón de cargar.
- Diálogo coherente con el tema (`ui-theming`).

**Non-Goals:**
- Confirmar al limpiar el key o al hacer Reset.
- Ofrecer "Cargar y eliminar" desde el diálogo.
- Una opción para desactivar el aviso.

## Decisions

**La regla reutiliza `CanPost`.** No se crea `HasPendingTime`. Alternativa: `TimeElapsed > 0` propio; descartada porque duplica la regla y podría divergir si cambia el umbral de carga.

**La confirmación vive en `MainWindow.RemoveIssue`, no en el modelo.** `IssueListViewModel.Remove` sigue siendo incondicional, así que tests y llamadores programáticos no cambian. Alternativa: usar `RemoveRequested`/`RequestRemove`; descartada, agrega indirección para un solo suscriptor y deja código muerto sin resolver.

**Ventana WPF propia (`ConfirmRemoveWindow`) en vez de `MessageBox`.** `MessageBox` es un diálogo nativo cuyo cuerpo no toma el tema. La ventana sigue el patrón de `EditTimeWindow`: `Background="{DynamicResource Background}"`, `SetTitleBarDarkMode` en `SourceInitialized`, `CenterOwner`, `ShowInTaskbar=False`.

**Botones Eliminar / Cancelar, con Cancelar como `IsDefault` e `IsCancel`.** Enter y Esc nunca eliminan. Se expone el resultado vía `ShowDialog() == true` para eliminar. La ventana recibe ya formateados el key (o vacío) y el tiempo (`JiraTimeHelpers.TimeSpanToDisplayTime`) y un flag de timer en curso; no conoce el modelo.

## Risks / Trade-offs

[Un click extra al quitar filas con tiempo] → Es el comportamiento pedido; las filas sin tiempo no se ven afectadas.
[El timer sigue corriendo mientras el diálogo está abierto] → El tiempo mostrado es una foto al abrir; el diálogo es modal y breve, y el mensaje no promete un valor exacto al segundo.
[Estilo del botón por defecto en el tema] → Reutilizar los estilos implícitos de `Styles.xaml`, como `EditTimeWindow`, y verificar en ambos temas.
