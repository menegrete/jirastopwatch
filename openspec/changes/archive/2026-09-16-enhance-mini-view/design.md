## Context

Ver `proposal.md` — Why. Lo que sigue son las restricciones del código actual
que condicionan el enfoque.

- `ActiveTimerViewModel` ([Model/ActiveTimerViewModel.cs](../../../source/StopWatch/Model/ActiveTimerViewModel.cs))
  ya expone `RunningSources` (todos los `ITimerSource` corriendo), pero
  `MiniTimerWindow` solo bindea sus propiedades singulares (`IssueKey`,
  `Summary`, `Elapsed`, `IsRunning`) — no hay hoy un ViewModel por fila.
- El tick de 1 segundo vive en `MiniTimerWindow`, no en el ViewModel (ver
  decisión D3 del change `add-mini-timer-view`, todavía vigente): la ventana
  llama `viewModel.Refresh()` cada segundo mientras está visible.
- El single-timer rule (`AllowMultipleTimers == false`) se aplica hoy en
  `MainWindow.issues_TimerStarted`, después de que el timer ya arrancó:
  `issues.PauseAllBut(issue)` pausa a los demás. Este handler es el único
  punto de entrada para *cualquier* arranque de timer, sea desde una fila de
  la ventana principal o desde `ActiveTimerViewModel.ToggleActive()` (que
  llama `ITimerSource.StartStop()`, documentado explícitamente como "raising
  TimerStarted so that the single-timer rule still applies when resuming").
  Es decir: ya existe un único lugar donde interceptar cualquier arranque,
  sin importar la vista de origen.
- El posicionamiento de `MiniTimerWindow` distingue dos sistemas de
  coordenadas (píxeles de pantalla vs. unidades independientes de WPF) y ya
  resuelve "pegar contra un borde" en `SnapAndRemember`/`SnapToEdges`
  ([Helpers/ScreenPlacement.cs](../../../source/StopWatch/Helpers/ScreenPlacement.cs)).
  No existe hoy el concepto de "borde de anclaje actual" como estado — se
  recalcula cada vez contra `Screen.WorkingArea`.
- `MainWindow.ExitMiniView` reaplica `restoreLeft`/`restoreTop` sin
  validarlos contra las pantallas conectadas, a diferencia de
  `MiniTimerWindow.ShowAt()`, que sí llama `ScreenPlacement.EnsureOnScreen` /
  `FallbackLocation` antes de mostrarse.

## Goals / Non-Goals

**Goals:**

- Que la vista mini muestre cada timer corriendo como una fila propia,
  reutilizando el layout visual de la fila única actual.
- Que el tope de timers simultáneos sea una única regla, aplicada en el mismo
  punto que ya aplica el single-timer rule, sin duplicarla por vista.
- Que la corrección de la posición fuera de pantalla reutilice el mecanismo
  que ya protege a `MiniTimerWindow`, en lugar de inventar uno nuevo para
  `MainWindow`.

**Non-Goals:**

- No se cambia el criterio de qué timer se pausa cuando `AllowMultipleTimers`
  está desactivado (`PauseAllBut` sigue igual). El tope configurable solo
  aplica cuando esa opción está activada.
- No se agrega scroll ni un tope duro a la cantidad de filas: en el uso real
  el máximo de timers corriendo ya está acotado por `MaxConcurrentTimers`
  (default 3), así que una lista sin scroll es suficiente.
- No se persiste "contra qué borde está anclada" la vista mini: se sigue
  derivando en el momento a partir de la posición actual, igual que hace
  `SnapAndRemember` hoy.

## Decisions

### D1. Un ViewModel por fila, alimentado por `RunningSources`

Se agrega `MiniTimerRowViewModel` (envuelve un `ITimerSource`, expone
`IssueKey`, `Summary`, `ElapsedText`, `IsRunning` y un comando de
pausar/reanudar) y `MiniTimerWindow` bindea un `ItemsControl` a una colección
de estos, reconstruida en cada `Refresh()` a partir de
`ActiveTimerViewModel.RunningSources` (o, si no hay ninguno corriendo, una
lista de un solo elemento con el issue resuelto por `ActiveTimerViewModel`
como hoy).

`ActiveTimerViewModel` no cambia su contrato — sigue resolviendo "el issue
activo" para el caso sin timers corriendo, y `RunningSources` para el caso con
varios. La única pieza nueva es la fila como unidad de presentación.

Alternativa considerada: que `ActiveTimerViewModel` mismo exponga la
colección de filas observable. Descartada porque mezclaría la resolución de
"cuál es el activo" (que también usa la ventana principal, indirectamente,
vía `NotifyTimerStarted`) con una responsabilidad de presentación que solo le
importa a la vista mini.

### D2. La dirección de expansión se deriva de la posición actual, no se persiste

Al recalcular el alto (cada vez que cambia la cantidad de filas), se compara
la posición actual del pill contra el `WorkingArea` de la pantalla que lo
contiene: si el borde inferior del pill está a `SnapThreshold` px o menos del
borde inferior del área de trabajo, se ancla el borde inferior (nuevas filas
empujan `Top` hacia arriba); si el superior está igual de cerca del borde
superior, se ancla el de arriba (nuevas filas se agregan hacia abajo, `Top`
fijo); si no está pegada a ningún borde vertical, se ancla arriba (crece hacia
abajo), que es el caso por defecto.

Reutiliza el mismo umbral que ya usa `SnapToEdges` en vez de introducir un
segundo concepto de "cerca de un borde". Igual que con la posición, el
resultado final se vuelve a pasar por `EnsureOnScreen` para no quedar fuera de
pantalla si el cálculo empuja el borde opuesto más allá del área de trabajo.

Alternativa considerada: guardar un flag explícito de a qué borde está
anclada la vista, actualizado en `SnapAndRemember`. Descartada: agrega estado
que hay que mantener sincronizado con la posición real, para resolver algo
que ya es derivable de la posición en el momento en que hace falta.

### D3. El botón de volver es un elemento superpuesto, no una columna de la fila

En el XAML, el `ItemsControl` con las filas y el botón de volver pasan a ser
hermanos dentro de un mismo `Grid` (en vez de que el botón sea una columna
más de la única fila, como hoy). El botón se posiciona con
`HorizontalAlignment="Right"` y `VerticalAlignment="Top"`, sobre el borde del
contenedor completo, y cada fila deja de tener una columna para él.

Consecuencia: el ancho de la fila que antes reservaba espacio para ese botón
ahora se comparte entre summary y tiempo; hay que revisar que la primera fila
no quede con el texto tapado por el botón superpuesto (margen/padding en la
esquina superior derecha del contenido).

### D4. Doble click se resuelve dentro del `MouseLeftButtonDown` existente

`MiniTimerWindow_MouseLeftButtonDown` ya es lo primero que se ejecuta al
presionar sobre el fondo (los controles internos consumen su propio click
antes). Se agrega, al principio del handler, un chequeo de
`e.ClickCount == 2`: si es doble click, se dispara `RestoreRequested` y se
retorna sin llamar a `DragMove()`. Si es simple, sigue el flujo actual
(arrastre + snap).

Alternativa considerada: un handler separado en `MouseDoubleClick`.
Descartada: `MouseLeftButtonDown` ya corre primero y ya decide si el evento
es "sobre el fondo"; agregar un segundo handler duplicaría esa decisión y
crearía una carrera entre "ya se llamó a `DragMove` para el primer click" y
"llegó el evento de doble click". Riesgo a validar a mano (no hay tests de UI
en el proyecto, ver design.md de `add-mini-timer-view`): confirmar que
`ClickCount` llega en 2 en el segundo `MouseLeftButtonDown` incluso habiendo
corrido un `DragMove()` bloqueante para el primer click.

### D5. El tope de timers simultáneos se aplica revirtiendo el arranque, en el mismo punto que el single-timer rule

`MainWindow.issues_TimerStarted` (el único punto de entrada para cualquier
arranque, sea desde una fila de la ventana principal o desde
`ActiveTimerViewModel.ToggleActive()`) pasa a chequear, cuando
`AllowMultipleTimers` está activado, si `issues.Running.Count()` supera
`settings.MaxConcurrentTimers`; si lo supera, pausa el que se acaba de
arrancar (el mismo `issue` que llegó al handler) en vez de dejarlo corriendo.

Esto reutiliza exactamente el patrón que ya existe para
`AllowMultipleTimers == false` (que también actúa después de que el timer ya
arrancó, pausando lo que corresponda) en lugar de agregar un segundo mecanismo
que intercepte *antes* de arrancar. Interceptar antes exigiría meterse en
`IssueViewModel.StartStop`/`WatchTimer.Start()`, que hoy no tienen ningún
punto de decisión "¿puedo arrancar?" — lo insertan y listo.

Efecto observable: al llegar al tope, el timer que se intentó arrancar queda
pausado de nuevo casi inmediatamente (revert), no directamente " no
arranca" — indistinguible para el usuario del resultado descrito en la spec,
pero relevante si en el futuro se anima la transición de estado.

Alternativa considerada: exponer un método `CanStart()` en
`IssueListViewModel` y consultarlo antes de invocar `StartStop()` desde cada
control de UI (`IssueControl`, `MiniTimerRowViewModel`, la ventana principal).
Descartada: multiplica los puntos que tienen que acordarse de preguntar, en
vez de mantener una única regla en el handler que ya centraliza el
single-timer rule.

### D6. La validación de posición al salir de la vista mini reutiliza `ScreenPlacement`

`MainWindow.ExitMiniView` pasa `restoreLeft`/`restoreTop` (en unidades WPF) y
el tamaño de la ventana por `ScreenPlacement.EnsureOnScreen` — convirtiendo a
píxeles de pantalla con el mismo patrón de `GetScale`/`PillLocation` que ya
usa `MiniTimerWindow` — antes de asignarlos a `Left`/`Top`. Si la posición no
cae en ninguna pantalla conectada, se usa
`ScreenPlacement.FallbackLocation(size)` contra la pantalla principal, igual
que hace `MiniTimerWindow.ShowAt()` con `MiniViewLocation`.

No se toca `ScreenPlacement` ni se le agrega ningún método: ambas ventanas ya
pueden llamar a las mismas funciones estáticas, solo que hasta ahora
`ExitMiniView` no lo hacía.

## Risks / Trade-offs

- **`e.ClickCount` tras un `DragMove()` bloqueante** (ver D4) → riesgo de que
  el segundo click de un doble click no llegue con `ClickCount == 2` si el
  bombeo de mensajes de WPF durante `DragMove()` interfiere con el contador
  de clicks del sistema. Mitigación: es el primer punto a verificar a mano al
  implementar; si no funciona, la alternativa de contingencia es medir el
  tiempo entre dos `MouseLeftButtonDown` consecutivos a mano.
- **Revertir en vez de bloquear** (ver D5) → un usuario que mire con atención
  puede notar el parpadeo de "arrancó y se pausó solo". Aceptado: es el mismo
  trade-off que ya existe para `AllowMultipleTimers == false`, y no se
  reportó como problema en ese caso.
- **El botón flotante superpuesto (D3) puede tapar texto en pantallas muy
  angostas** si el summary de la primera fila es largo → mitigado con el
  mismo `TextTrimming="CharacterEllipsis"` que ya usa el summary, más margen
  derecho suficiente para el botón.
- **Sin tests automatizados de UI** (situación ya aceptada en el change
  anterior) → la verificación de las cinco decisiones de este documento es
  manual: expandir/contraer filas, anclaje contra cada borde, doble click,
  tope de timers desde ambas vistas, y desconectar un monitor con la vista
  mini activa para probar D6.

## Migration Plan

Sin migración de datos. `MaxConcurrentTimers` es un setting nuevo con default
3 — un usuario existente con `AllowMultipleTimers = true` pasa a tener ese
tope la primera vez que abre la aplicación con esta versión, sin acción de su
parte.

Rollback: revertir los archivos tocados (ver proposal.md — Impact) deja el
comportamiento exactamente como estaba; no hay estado persistido que haya que
limpiar aparte del propio setting nuevo, que simplemente deja de leerse.
