# Proposal

## Why

Con "permitir varios timers" activado, pausar un timer desde la vista mini hace que su fila desaparezca, porque la vista mini lista solo los timers que están corriendo. El timer pausado sigue acumulando tiempo que aún no se cargó a Jira, y desde la vista mini ya no se puede reanudar ni ver: hay que volver a la ventana completa. Ver el problema en [#56](https://github.com/menegrete/jirastopwatch/issues/56).

## What Changes

- Con "permitir varios timers" **activado**, la vista mini lista cada timer corriendo **más** cada timer pausado que todavía tiene tiempo acumulado (pendiente de cargar). La fila de un timer pausado se muestra atenuada y conserva su control de reanudar.
- Una fila pausada sale de la vista mini cuando su tiempo deja de estar pendiente, es decir, cuando el timer vuelve a cero (por ejemplo, tras cargar el tiempo a Jira o al resetearlo).
- Las filas siguen el orden de la ventana principal: pausar una fila no la mueve de lugar.
- Con "permitir varios timers" **desactivado**, no cambia nada: la vista mini muestra una sola fila (el timer corriendo o, si ninguno corre, el último que corrió).
- No se agrega un tope propio para las filas pausadas; el límite sigue siendo el de timers simultáneos, que solo cuenta los que corren.
- El widget de la barra de tareas no cambia.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `mini-timer-view`: cambia qué filas muestra la vista mini cuando se permiten varios timers (corriendo más pausados con tiempo pendiente) y cómo se presenta una fila pausada.

## Impact

- `ActiveTimerViewModel` (`Model/ActiveTimerViewModel.cs`): nueva lista de fuentes que la vista mini debe mostrar; recibe por constructor un `Func<bool>` que indica si se permiten varios timers. `RunningSources` queda intacta.
- `AppComposition` (`Model/AppComposition.cs`): pasa el valor de `Settings.AllowMultipleTimers` al construir el modelo.
- `MiniTimerWindow` (`UI/MiniTimerWindow.xaml(.cs)`): `ResolveSources()` usa la lista nueva; la fila pausada se atenúa.
- `StopWatchTest`: pruebas del modelo para los casos corriendo, pausado con tiempo, pausado en cero y modo de un solo timer.
- Sin cambios en settings persistidos, en el contrato de plugins ni en el widget de la barra de tareas.
