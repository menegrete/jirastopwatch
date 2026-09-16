## Why

La vista mini hoy solo muestra un timer a la vez (el "activo"), con un simple
indicador `+1` cuando hay otros corriendo — deja al usuario sin saber cuáles
son ni cuánto llevan. Además, volver a la ventana completa no tiene un gesto
rápido (doble click) y, si el monitor donde estaba la ventana principal se
desconecta mientras se usa la vista mini, al volver la ventana reaparece fuera
de pantalla y la única salida es reiniciar la aplicación.

## What Changes

- La vista mini pasa a listar todos los timers corriendo (una fila por timer,
  con su key, summary, tiempo y control de pausa/reanudar propio) en lugar de
  mostrar uno solo con un badge `+1`. Con un único timer corriendo se ve una
  sola fila, sin cambio de layout.
- La lista crece alejándose del borde de pantalla contra el que está anclada
  la vista mini, para no salirse de la pantalla.
- El control de volver a la ventana completa deja de repetirse por fila: pasa
  a ser un único botón flotante, superpuesto en una esquina del contenedor,
  visible sin importar cuántas filas haya.
- Doble click en el fondo de la vista mini también vuelve a la ventana
  completa, además del botón existente.
- Al volver a la ventana completa, su posición guardada se valida contra las
  pantallas conectadas antes de aplicarla; si la pantalla original ya no está
  disponible, se usa una posición visible de la pantalla principal — el mismo
  mecanismo que ya protege la posición de la propia vista mini.
- Nuevo setting `MaxConcurrentTimers` (entero, default 3), habilitado
  únicamente cuando "permitir múltiples timers" está activado. Al llegar al
  tope, intentar arrancar un timer adicional no tiene efecto — ni desde la
  ventana principal ni desde la vista mini — hasta que el usuario pause alguno
  a mano.

## Capabilities

### New Capabilities

- `concurrent-timer-limit`: tope configurable a la cantidad de timers que
  pueden correr simultáneamente cuando "permitir múltiples timers" está
  activado, aplicado por igual desde la ventana principal y la vista mini.

### Modified Capabilities

- `mini-timer-view`: pasa de mostrar un timer con indicador de "hay otros" a
  listar todos los timers corriendo; el control de volver se independiza de
  las filas; se agrega el doble click como gesto de volver; y la posición
  guardada de la ventana principal se valida contra las pantallas conectadas
  al restaurarla.

## Impact

- `source/StopWatch/Model/ActiveTimerViewModel.cs`: ya expone `RunningSources`;
  puede necesitar exponer el estado de "en el tope" para la ventana principal
  y la vista mini.
- `source/StopWatch/UI/MiniTimerWindow.xaml` y `.xaml.cs`: template por fila en
  lugar de binding único, cálculo de dirección de expansión, botón de volver
  flotante, doble click.
- `source/StopWatch/UI/MainWindow.xaml.cs`: validar `restoreLeft`/`restoreTop`
  contra `ScreenPlacement.EnsureOnScreen`/`FallbackLocation` en `ExitMiniView`;
  aplicar el tope de timers en `issues_TimerStarted`.
- `source/StopWatch/Model/IssueListViewModel.cs`: `PauseAllBut` o una nueva
  regla equivalente para el tope configurable.
- `source/StopWatch/Settings/Settings.cs`, `Properties/Settings.settings`,
  `Properties/Settings.Designer.cs`: nuevo setting `MaxConcurrentTimers`.
- `source/StopWatch/UI/SettingsWindow.xaml` y `.xaml.cs`: control numérico para
  `MaxConcurrentTimers`, habilitado junto al checkbox existente.
