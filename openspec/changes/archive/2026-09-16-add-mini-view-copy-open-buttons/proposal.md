## Why

La vista mini (`MiniTimerWindow`) muestra el key y el summary de cada timer, pero no ofrece ninguna acción sobre ellos: para copiar el key o abrir el issue en el navegador el usuario tiene que volver a la ventana principal. Esas dos acciones ya existen ahí (`copy-issue-key`, `issue-open-action`); llevarlas a la vista mini evita ese ida y vuelta para el caso de uso que la vista mini existe para cubrir: dejarla corriendo mientras se trabaja en otra cosa.

## What Changes

- Cada fila de la vista mini ofrece, al pasar el mouse sobre su key (no sobre toda la fila), un ícono para copiar ese key y un ícono para abrir el issue en el navegador. Ambos ocultos por defecto, sin ocupar espacio.
- Al aparecer, esos íconos comprimen el espacio disponible para el summary de esa fila (que ya se trunca cuando no entra); al dejar de hoverear la key, o inmediatamente después de completar la acción, vuelven a ocultarse y el summary recupera su ancho.
- Copiar el key confirma visualmente con el mismo patrón que la ventana principal (el ícono cambia a un check momentáneamente).
- Abrir en el navegador respeta la misma regla que ya rige esa acción en la ventana principal: solo está disponible si el summary de esa fila ya se resolvió contra Jira.
- La lógica de confirmación visual de copiado se extrae de `MainWindow.xaml.cs` a un helper compartido para que la vista mini la reutilice en vez de duplicarla.

## Capabilities

### New Capabilities

(ninguna)

### Modified Capabilities

- `copy-issue-key`: quita la restricción "alcance limitado a la ventana principal" y agrega el comportamiento de copiar el key propio desde la vista mini, con su propio disparador (hover sobre la key de la fila, no sobre la fila entera).
- `mini-timer-view`: cada fila pasa a ofrecer copiar su key y abrir su issue en el navegador, con la regla de habilitación existente de `issue-open-action`.

## Impact

- `source/StopWatch/UI/MiniTimerWindow.xaml` (nuevos controles por fila) y `MiniTimerWindow.xaml.cs` (handlers).
- `source/StopWatch/UI/MainWindow.xaml.cs`: se extrae `FlashCopyConfirmation` (y la construcción de la URL de "abrir en el navegador") a un helper compartido; sin cambio de comportamiento en la ventana principal.
- `source/StopWatch/Model/MiniTimerRowViewModel.cs`: sin cambios - el key, el summary (para inferir si el issue está resuelto) y la referencia al `ITimerSource` ya alcanzan.
- `openspec/specs/copy-issue-key/spec.md` y `openspec/specs/mini-timer-view/spec.md`: specs delta.
