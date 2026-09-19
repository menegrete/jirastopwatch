## Why

GitHub issue #14 pide un modo de minimizado que se vea como un widget nativo
de la barra de tareas de Windows, similar a los widgets de "now playing" que
se anclan junto al reloj. Hoy `minimize-behavior` solo ofrece `Mini View`
(ventana flotante libre) y `Tray` (ícono de bandeja sin contenido visible);
ninguna de las dos deja el timer activo a la vista sin ocupar una ventana
propia en la barra de tareas.

## What Changes

- Nueva vista `Taskbar Widget`: una ventana WPF sin bordes, anclada al área
  libre de la barra de tareas de Windows (junto al botón de widgets/reloj o
  junto al final de los íconos, según la alineación de la taskbar), que
  muestra una sola fila con el timer activo.
- Sigue el comportamiento nativo de la barra de tareas: se esconde y
  reaparece en sincronía cuando la taskbar tiene auto-hide activado, y se
  ancla al monitor(es) que el usuario elija.
- Interacción: botón para pausar/reanudar el timer activo, clic derecho abre
  el mismo menú de contexto que ya existe, doble clic restaura la ventana
  principal (mismo mecanismo que `Mini View`).
- Tercer valor `TaskbarWidget` en el setting `Minimize Behavior`
  (`Settings.MinimizeBehavior`), junto a `MiniView` y `Tray`.
- Submenú de selección de monitor(es) para el widget, análogo a un menú de
  monitores existente en la referencia tomada como base (issue #14 linkea
  `now-playing-taskbar-widget`).
- Mientras `TaskbarWidget` está seleccionado, la opción "permitir múltiples
  timers" queda deshabilitada en Configuración (el widget solo tiene sentido
  con un único timer activo a la vez).
- La opción `Taskbar Widget` solo aparece en Configuración en Windows (mismo
  gate que ya oculta el control de `Minimize Behavior` completo en
  plataformas no-Windows).

## Capabilities

### New Capabilities

- `taskbar-widget-view`: la ventana anclada a la barra de tareas que muestra
  el timer activo — su contenido, su anclaje/seguimiento de la taskbar
  (incluyendo auto-hide y multi-monitor), y sus interacciones (pausar,
  reanudar, menú contextual, restaurar la ventana principal).

### Modified Capabilities

- `minimize-behavior`: agrega la tercera opción `Taskbar Widget` al control
  de Configuración, la regla de exclusión mutua con "permitir múltiples
  timers", y la visibilidad de la opción solo en Windows.

## Impact

- **Código nuevo**: una ventana WPF (`UI/TaskbarWidgetWindow.xaml(.cs)`),
  helpers de posicionamiento/interop con la barra de tareas de Windows
  (P/Invoke: `SHAppBarMessage`, UI Automation para ubicar el hueco libre,
  `WinEventHook` para seguir la animación de auto-hide, `SetWindowRgn` para
  recortar durante el deslizamiento), siguiendo el mismo split entre
  matemática pura testeable e interop sucio que ya usa
  `Helpers/ScreenPlacement.cs`.
- **Código modificado**: `Settings.cs`/`Properties/Settings.settings` (nuevo
  valor de enum `MinimizeBehavior` + setting de monitores elegidos),
  `SettingsWindow.xaml(.cs)` (tercera opción en el combo, deshabilitar
  "permitir múltiples timers", submenú de monitores), `MainWindow.xaml.cs`
  (activar/salir del modo igual que hace con `MiniTimerWindow` hoy).
- **Sin impacto** en `IssueListViewModel`/`ActiveTimerViewModel`/`WatchTimer`
  — la nueva vista consume `ActiveTimerViewModel` igual que `MiniTimerWindow`,
  sin cambios al modelo.
- **Dependencia externa nueva**: `System.Windows.Automation` (UI Automation)
  para ubicar los botones de la barra de tareas.
