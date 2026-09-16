## 1. Setting: máximo de timers simultáneos

- [x] 1.1 Agregar `MaxConcurrentTimers` (int, default 3) a
      `Properties/Settings.settings` y regenerar `Settings.Designer.cs`
- [x] 1.2 Exponer `MaxConcurrentTimers` en `Settings.cs` (leer en
      `ReadSettings`, escribir en `Save`), igual que `AllowMultipleTimers`
- [x] 1.3 Agregar el control numérico en `SettingsWindow.xaml`, habilitado
      solo cuando `cbAllowMultipleTimers` está tildado
- [x] 1.4 Cargar y guardar el valor en `SettingsWindow.xaml.cs`

## 2. Tope de timers simultáneos (regla de modelo)

- [x] 2.1 En `MainWindow.issues_TimerStarted`, cuando `AllowMultipleTimers`
      es `true` y `issues.Running.Count()` supera `settings.MaxConcurrentTimers`
      después del arranque, pausar el issue recién arrancado (ver design.md
      D5)
- [x] 2.2 Verificar que el mismo chequeo cubre el arranque desde
      `ActiveTimerViewModel.ToggleActive()` (vista mini) sin cambios
      adicionales, ya que ambos casos pasan por `issues_TimerStarted`
- [x] 2.3 Comunicar al usuario por qué no arrancó (tooltip o indicación
      equivalente a la que ya explica por qué el botón de agregar issue no
      hace nada)

## 3. Vista mini: una fila por timer corriendo

- [x] 3.1 Crear `MiniTimerRowViewModel` (envuelve un `ITimerSource`; expone
      `IssueKey`, `Summary`, `ElapsedText`, `IsRunning` y el comando de
      pausar/reanudar)
- [x] 3.2 En `MiniTimerWindow`, construir la colección de filas en cada
      `Refresh()`: una por cada `ActiveTimerViewModel.RunningSources`, o una
      sola con el issue resuelto por `ActiveTimerViewModel` si no hay ninguno
      corriendo
- [x] 3.3 Reemplazar el binding único del `Grid` actual por un `ItemsControl`
      con un `DataTemplate` por fila (stripe, key, summary, tiempo, toggle),
      sin la columna de badge `+1`
- [x] 3.4 Verificar que con una sola fila el tamaño y la disposición quedan
      igual que antes del cambio

## 4. Vista mini: expansión y botón de volver

- [x] 4.1 Mover el botón de volver a un elemento superpuesto en la esquina
      superior derecha del contenedor completo (fuera del `ItemsControl`),
      con margen suficiente para no tapar el summary de la primera fila
- [x] 4.2 Al cambiar la cantidad de filas, recalcular el alto de la ventana y
      determinar el borde de anclaje comparando la posición actual contra
      `Screen.WorkingArea` con el mismo umbral que usa `SnapToEdges` (ver
      design.md D2)
- [x] 4.3 Ajustar `Top` para mantener fijo el borde inferior cuando está
      anclada abajo, o mantener fijo el borde superior en cualquier otro caso
- [x] 4.4 Pasar el resultado por `ScreenPlacement.EnsureOnScreen` antes de
      aplicarlo, para no salirse de pantalla al crecer

## 5. Vista mini: doble click para volver

- [x] 5.1 En `MiniTimerWindow_MouseLeftButtonDown`, chequear
      `e.ClickCount == 2` al principio: si es doble click, disparar
      `RestoreRequested` y retornar sin llamar a `DragMove()`
- [x] 5.2 Verificar a mano que el segundo click de un doble click llega con
      `ClickCount == 2` después de que el primero ya ejecutó un `DragMove()`
      bloqueante (riesgo señalado en design.md)

## 6. Fix: posición fuera de pantalla al volver a la ventana completa

- [x] 6.1 En `MainWindow.ExitMiniView`, antes de asignar `Left`/`Top`,
      convertir `restoreLeft`/`restoreTop` y el tamaño a píxeles de pantalla
      y validarlos con `ScreenPlacement.EnsureOnScreen`
- [x] 6.2 Si la posición no cae en ninguna pantalla conectada, usar
      `ScreenPlacement.FallbackLocation` contra la pantalla principal
      (mismo mecanismo que ya usa `MiniTimerWindow.ShowAt()`)

## 7. Verificación manual

- [x] 7.1 Un timer corriendo: la vista mini se ve igual que antes del cambio
- [x] 7.2 Tres timers corriendo (con `AllowMultipleTimers` y
      `MaxConcurrentTimers = 3`): la vista mini muestra las tres filas, cada
      una con su propio control de pausa/reanudar
- [x] 7.3 Intentar arrancar un cuarto timer estando en el tope, desde la
      ventana principal y desde la vista mini: en ambos casos no arranca y
      los demás siguen corriendo
- [x] 7.4 Anclar la vista mini contra cada borde de la pantalla (arriba,
      abajo, y sin anclar) y pasar de uno a varios timers corriendo: la lista
      crece alejándose del borde correspondiente y no se sale de pantalla
- [x] 7.5 Doble click sobre el fondo de la vista mini vuelve a la ventana
      completa, con uno y con varios timers mostrados
- [x] 7.6 Con múltiples monitores: activar la vista mini con la ventana
      principal en el monitor secundario, desconectar ese monitor, y volver a
      la vista completa — la ventana debe reaparecer visible en el monitor
      principal sin reiniciar la aplicación
