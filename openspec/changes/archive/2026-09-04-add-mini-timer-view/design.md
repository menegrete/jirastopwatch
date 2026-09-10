## Context

Ver `proposal.md` — Why. Lo que sigue son las restricciones del código actual
que condicionan el enfoque.

- El proyecto está en `net10.0-windows`, SDK-style, `UseWindowsForms`, con
  `TreatWarningsAsErrors=true` y gestión centralizada de paquetes.
- **El refresco es de 30 segundos.** `MainForm.ticker` usa
  `defaultDelay = 30000` y en cada tick hace tres cosas juntas: consultar Jira,
  refrescar la salida de todos los `IssueControl` y guardar settings. No hay un
  refresco de UI separado del de red.
- **No existe un modelo del "timer activo".** Para saber cuál timer corre hay
  que recorrer `MainForm.issueControls` preguntando `WatchTimer.Running`, que es
  exactamente lo que hace `HandleSessionLock`. `lastRunningIssue` guarda ese
  resultado, pero solo para el caso de bloqueo de sesión.
- **El tema en WinForms cuesta 579 líneas.** `ThemeApplier` es el cuarto archivo
  más grande del proyecto y su contenido son workarounds: dibujar a mano los
  glifos de checkbox y radio, pintar el borde de los `TextBox` en el control
  padre, `OwnerDrawFixed` en los combos, más interop con APIs no documentadas de
  `uxtheme` (`SetWindowTheme("DarkMode_CFD")`, `SetPreferredAppMode` resuelta por
  ordinal 135).
- **La lógica de UI está en el code-behind.** `IssueControl` mezcla en 869
  líneas layout, llamadas a Jira, lógica de timer, clipboard y transiciones. No
  hay capa de ViewModel ni tests de UI.
- El repositorio es un fork de `jirastopwatch/jirastopwatch`, pero upstream está
  en modo mantenimiento (los últimos meses son bumps de dependabot y dos cambios
  de `FormBorderStyle`) y no se hacen aportes hacia arriba. **No hay que
  preservar compatibilidad de merge con upstream.**

## Goals / Non-Goals

**Goals:**

- Que la vista mini se vea como una aplicación moderna —esquinas redondeadas,
  transparencia, sin marco de sistema— sin escribir código de dibujado a mano.
- Que el estado del timer activo tenga un dueño único y observable, en lugar de
  recorrer controles desde varios lugares.
- Que la vista mini sirva de plantilla del patrón que se quiera usar de acá en
  adelante, no de anexo descartable.
- Que sea reversible: si el enfoque no convence, se descarta sin haber tocado
  nada de la ventana principal más allá del botón de activación.

**Non-Goals:**

- No se migran las ventanas existentes. Eso se discute después, con el resultado
  de este change en la mano (ver "Futuro" abajo).
- No se toca `WatchTimer` ni la lógica de posteo de worklogs.
- No se reserva espacio de escritorio como lo hace la barra de tareas.
- No se registra un atajo de teclado global del sistema.

## Decisions

### D1. La vista mini se implementa en WPF, no en WinForms

`<UseWPF>true</UseWPF>` convive con `UseWindowsForms` en el mismo csproj sobre
`net10.0-windows`. La vista mini es una `Window` WPF independiente, no un
control embebido, así que no hace falta `ElementHost` ni `WindowsFormsHost`.

Lo que se necesita para una ventana flotante —esquinas redondeadas, alpha por
píxel, sombra, aparición y desaparición con fundido— en WPF son propiedades
(`CornerRadius`, `AllowsTransparency`, `DropShadowEffect`, `Storyboard`). En
WinForms es `UpdateLayeredWindow` y dibujar todo en `OnPaint`, o aceptar un
rectángulo gris con `TransparencyKey` y bordes con artefactos. El proyecto ya
tiene 579 líneas de evidencia de lo que cuesta pelearse con el renderizado de
WinForms.

Alternativas consideradas:

- **WinForms.** Cero fricción de build y coherente con el resto. Descartada:
  todo el valor de la feature es visual, y es justo donde WinForms cobra más
  caro.
- **Rainmeter** (skin `.ini` + bangs por `WM_COPYDATA` a la ventana
  `DummyRainWClass`). Técnicamente el camino más corto —Rainmeter ya resuelve
  siempre-encima, snap, arrastre, transparencia y persistencia de posición— y
  la comunicación es P/Invoke directo, sin spawnear procesos. Descartada como
  implementación: obliga al usuario a instalar otra aplicación, queda fuera del
  sistema de temas propio y convierte cualquier problema de la feature en
  debugging de configuración de Rainmeter. Sigue siendo viable **como adaptador
  opcional** una vez que exista `ActiveTimerViewModel` (ver Futuro).
- **AppBar real** (`SHAppBarMessage` con `ABM_NEW` / `ABM_SETPOS`). Es el único
  mecanismo que hace que las ventanas maximizadas no tapen nunca la vista mini.
  Descartada: hay que manejar `ABN_POSCHANGED`, cambios de resolución,
  multi-monitor y DPI, y si el proceso termina sin `ABM_REMOVE` el usuario queda
  con una franja de escritorio inutilizable hasta reiniciar la sesión. El costo
  y ese modo de falla no se justifican frente a `Topmost`.
- **Solo un icono dinámico en la bandeja.** Cero superficie ocupada y ya existe
  el `NotifyIcon`. Descartada como única opción: en 16×16 px no entra un
  cronómetro legible, el tiempo real queda en el tooltip —que exige hover— y en
  Windows 11 el icono puede terminar en el área de desbordamiento.

### D2. `ActiveTimerViewModel` es la única fuente de la vista mini

Un objeto observable (`INotifyPropertyChanged`) con `IssueKey`, `Summary`,
`Elapsed`, `IsRunning` y `HasOtherRunningTimers`, más comandos de pausar y de
volver. `MainForm` lo alimenta cuando cambia el conjunto de issues o cuando un
timer arranca o se pausa; la vista mini solo bindea.

El recorrido de `issueControls` buscando el timer corriendo pasa a vivir acá, y
`HandleSessionLock` puede consumirlo en lugar de repetirlo.

Alternativa considerada: que la vista mini reciba el `IssueControl` y lea sus
propiedades. Descartada porque acopla una ventana WPF a un `UserControl` de
WinForms —justo el acoplamiento que haría descartable este trabajo— y porque
deja la regla de "cuál es el issue activo" duplicada.

### D3. El tick de UI se separa del tick de red

`MainForm.ticker` (30 s) sigue haciendo lo que hace: Jira, refresco general y
guardado de settings. Se agrega un tick de 1 s cuya única responsabilidad es
recalcular `Elapsed` en el ViewModel, activo **solo mientras la vista mini está
visible y hay un timer corriendo**.

No se sube la frecuencia del ticker existente: eso multiplicaría por 30 las
consultas a Jira y los guardados de settings para resolver un problema de
presentación.

**El tick vive en la ventana, no en el ViewModel** (ajustado durante la
implementación). `ActiveTimerViewModel` no tiene timer propio: expone
`Refresh()` y quien lo muestre lo llama a la frecuencia que necesite. La vista
mini usa un `DispatcherTimer` de 1 s; la ventana principal llama a `Refresh()`
en los eventos de issues. Tres razones:

- El ViewModel queda libre de tipos de UI, así que sus tests no necesitan bomba
  de mensajes ni `UseWindowsForms` en el proyecto de test.
- `DispatcherTimer` dispara en el thread que posee la ventana WPF, que es el
  thread correcto para los bindings.
- "Activo solo mientras la vista mini está visible" sale por construcción si el
  timer pertenece a la ventana, en vez de tener que coordinarse con ella.

Como efecto secundario, cualquier consumidor futuro —el adaptador Rainmeter de
la sección Futuro, por ejemplo— maneja su propia cadencia sin pelearse con la
de la vista mini.

### D4. `Topmost` con snap magnético, no dock del sistema

La ventana es `Topmost` con `ShowInTaskbar = false`. El arrastre se resuelve
llamando a `DragMove()` en `MouseLeftButtonDown` sobre el fondo, y el snap se
evalúa al terminar el arrastre contra `Screen.WorkingArea` de la pantalla que
contiene el puntero. Usar `WorkingArea` y no `Bounds` es lo que garantiza que
no quede detrás de la barra de tareas.

La distancia de imantación y el criterio de "borde" son detalles de
implementación; lo observable está en la spec.

### D5. La ventana principal se esconde con `Hide()`

`Hide()` la saca también de la barra de tareas, que es el punto de la feature:
si quedara minimizada, habría dos representaciones simultáneas de la misma
aplicación. Antes de esconderla se guardan `Location`, `Size` y `WindowState`
para restaurarlos exactamente.

El riesgo de esto es que la aplicación quede inalcanzable si la vista mini
aparece fuera de la pantalla; lo cubre el requerimiento de validar la posición
guardada contra las pantallas disponibles.

### D6. El tema de la vista mini se resuelve mapeando `Theme.Current`

`Theme` ya expone una paleta semántica (`Background`, `Surface`, `Text`,
`TimerRunning`, `Border`, …) y es la fuente de verdad del tema en el resto de la
aplicación. La vista mini expone esos mismos valores como `SolidColorBrush` en
sus recursos, convirtiendo de `System.Drawing.Color` a `System.Windows.Media.Color`.

Así la vista mini queda coherente con las ventanas WinForms por construcción, y
no aparece un segundo origen de colores.

El tema Fluent nativo de WPF (`ThemeMode="Dark" | "Light" | "System"`) es
tentador pero **no se usa para esta ventana**, por dos razones: la paleta Fluent
no coincide con `Theme` y produciría una vista mini de otro color que el resto
de la aplicación; y la API es experimental (`WPF0001`), lo que con
`TreatWarningsAsErrors` obliga a un `NoWarn`. Se agrega igual ese `NoWarn` al
csproj porque el spike de la tarea 1 lo necesita para medir si `ThemeMode`
funciona en este contexto — ese dato es la entrada de la discusión de migración,
no de este change.

### D7. La posición se persiste en `Properties.Settings`, no en el blob de issues

`MiniViewLocation` (`string`, formato `"x,y"`) siguiendo el patrón de las demás
propiedades de `Settings.cs`: leída en `ReadSettings`, escrita en `Save`. No se
toca la serialización JSON de `PersistedIssues`, que resuelve otro problema y ya
carga con la migración del blob binario.

### D8. La vista mini muestra el tiempo con segundos, en otro formato que la ventana principal

Detectado durante la implementación. `JiraTimeHelpers.TimeSpanToJiraTime` tiene
**resolución de minutos** (`"1h 23m"`, `"0m"`): es la notación que Jira espera en
un worklog, y es la correcta para la ventana principal. Pero con ese formato el
requerimiento "el tiempo mostrado cambia visiblemente segundo a segundo" es
imposible de cumplir: la pantalla quedaría congelada 60 segundos por vez.

Se agrega `JiraTimeHelpers.TimeSpanToClockTime`, que formatea como cronómetro:
`"1:23:45"` pasada la hora, `"23:45"` por debajo. La vista mini usa este
formato; la ventana principal y todo lo que va a Jira siguen con
`TimeSpanToJiraTime`, intacto.

Consecuencia aceptada: el mismo tiempo se ve escrito distinto en las dos vistas.
Es lo correcto — una es un cronómetro en vivo y la otra es la cifra que se va a
registrar, redondeada al minuto como Jira la va a recibir.

## Risks / Trade-offs

- ~~**`Application.Current` es `null` en un proceso WinForms**~~ → **despejado
  por el spike de la tarea 1.** Con `Application.Current == null`, una `Window`
  WPF mostrada desde dentro de la bomba de mensajes de WinForms queda
  `IsLoaded`, `IsVisible`, con `PresentationSource` adjunto, con tamaño real
  (220×36) y renderiza: 7886 de 7920 píxeles opacos, donde los 34 transparentes
  son las cuatro esquinas de `CornerRadius=8`. `AllowsTransparency = true` con
  `WindowStyle = None` se acepta sin excepción. D6 no cambia.
- **Una ventana WPF modeless no recibe teclado dentro de una bomba de mensajes
  de WinForms** → hallazgo del spike, no estaba previsto. Hay que llamar a
  `System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(window)`
  después de `Show()` para que los eventos de teclado lleguen a la ventana. El
  spike confirmó que la llamada está disponible y se acepta en este contexto.
  Afecta poco a la vista mini porque se opera con mouse, pero sin esto ni
  `Esc`, ni `Tab`, ni la barra espaciadora sobre un botón enfocado funcionarían.
- **Dos frameworks de UI en el mismo proceso** → aumenta el tamaño del binario y
  el tiempo de arranque en frío la primera vez que se instancia algo de WPF.
  Mitigación: la ventana se crea de forma diferida, la primera vez que el
  usuario activa la vista mini, no al arrancar la aplicación.
- **`Topmost` no es a prueba de todo**: aplicaciones en pantalla completa
  exclusiva (juegos, algunas presentaciones) van a tapar la vista mini. Es una
  limitación aceptada; el único remedio sería D1/AppBar, descartado.
- **El snap no es un dock**: una ventana maximizada se dibuja debajo de la vista
  mini, no al costado. Aceptado.
- **Sin tests automatizados de UI** → la verificación es manual, y las
  situaciones a cubrir a mano están enumeradas en los escenarios de la spec.
- **`ThemeApplier` no alcanza la ventana WPF** → si en el futuro se agregan
  colores a `Theme`, hay dos lugares que consumen la paleta. Aceptado mientras
  sea una sola ventana; deja de ser aceptable si aparecen varias.

## Migration Plan

Sin migración de datos. La secuencia de implementación está en `tasks.md`; el
orden importa por una razón: la tarea 1 es un spike que puede cambiar D6, y las
tareas del ViewModel (2) tienen valor propio incluso si la vista mini se
descarta.

Rollback: la feature es aditiva. Quitar el botón de activación la deja
inaccesible sin afectar nada más; revertir `UseWPF` y borrar los dos archivos
nuevos la elimina por completo.

## Futuro: migrar las ventanas existentes a WPF

Fuera del alcance de este change, pero registrado acá para no perder el
razonamiento.

Los argumentos a favor de migrar todo son concretos: se borrarían `ThemeApplier`
(579 líneas de workarounds) y `ComboTextBoxEvents` (subclassing Win32 para
capturar `WM_PASTE`), más la interop no documentada de `uxtheme` en
`NativeMethods`. Cerca de 700 líneas cuya única razón de existir es que WinForms
no sabe hacer temas.

Los argumentos en contra ya no incluyen upstream, que está en mantenimiento.
Quedan dos:

1. No hay camino mecánico. Son ~3.200 líneas de forms y designers a reescribir.
2. **El trabajo real no es traducir XAML, es extraer los ViewModels.** La lógica
   está en el code-behind y no hay tests de UI que sostengan la reescritura.

**Dato del spike (tarea 1.5), como entrada de esta discusión:** asignar
`window.ThemeMode = System.Windows.ThemeMode.Dark` **no lanza excepción** con
`Application.Current == null` y la propiedad se lee de vuelta como `Dark`.
Cuidado con qué prueba eso y qué no: prueba que la API es usable en este
contexto, **no** que los diccionarios Fluent se hayan cargado y aplicado —
verificarlo requiere una ventana con controles Fluent reales y compararla
visualmente, que es trabajo del paso 2. Además, confirmado el choque de nombres
previsto: dentro del namespace `StopWatch`, `ThemeMode` resuelve a
`StopWatch.ThemeMode`, así que el tipo de WPF hay que escribirlo calificado
como `System.Windows.ThemeMode`.

De ahí la secuencia propuesta, con puntos de salida en cada paso:

| Paso | Qué | Riesgo | Valor propio |
|---|---|---|---|
| 1 | Este change: `ActiveTimerViewModel` + vista mini en WPF | bajo | la feature |
| 2 | `AboutForm` (48 líneas) y `EditTimeForm` (77) a WPF | bajo | menos `ThemeApplier` |
| 3 | Extraer ViewModels de `IssueControl` y `MainForm`, **sin tocar la vista** | medio | código testeable por primera vez |
| 4 | `MainWindow.xaml` + `IssueView.xaml`; borrar `ThemeApplier` | alto | −700 líneas de hacks |

El paso 3 es el que conviene priorizar aunque el 4 nunca se haga: dejaría
`IssueControl` en torno a 300 líneas y con lógica cubrible por tests.

**Cómo resultó trabajar en WPF acá (tarea 7.3).** El paso 1 no encontró
fricción: `UseWPF` junto a `UseWindowsForms` compiló sin tocar nada más, la
ventana convive con la bomba de mensajes de WinForms y el `Release` sigue en
cero warnings con `TreatWarningsAsErrors`. Lo que en WinForms hubiera sido
dibujado a mano —esquinas redondeadas, alpha por píxel, sombra, hover y pressed
de los botones— salió en XAML declarativo, y el equivalente de `ThemeApplier`
para esta ventana son las ~100 líneas de `ThemeBrushes` que solo traducen la
paleta, sin un solo workaround.

Tres fricciones concretas, todas menores y ya resueltas:

- La clase que genera el XAML es `public`; con un ViewModel y un `Settings`
  internos hay que poner `x:ClassModifier="internal"` en el `Window`.
- Hay dos sistemas de coordenadas en juego: `Screen.WorkingArea` está en píxeles
  físicos y `Left`/`Top`/`Width`/`Height` de WPF en unidades independientes de
  dispositivo. Todo el posicionamiento necesita conversión explícita por DPI.
- Una ventana WPF modeless no recibe teclado sin
  `ElementHost.EnableModelessKeyboardInterop`.

Ninguna de las tres es un argumento contra migrar; las tres son cosas que se
aprenden una vez. La conclusión práctica es que el paso 2 (AboutForm y
EditTimeForm) es de riesgo bajo y se puede encarar cuando haya ganas.

Una vez que exista `ActiveTimerViewModel`, también queda a un paso el
**adaptador Rainmeter** descartado en D1: enviar bangs por `WM_COPYDATA` desde
el ViewModel son unas 40 líneas y un `.ini` de ejemplo, como feature opt-in para
quien ya use Rainmeter.

## Open Questions

- ¿Conviene sumar un atajo de teclado para entrar y salir de la vista mini?
  `MainForm.ProcessCmdKey` ya maneja `Ctrl+P`, `Ctrl+L` y `Ctrl+E`, así que
  agregar uno es trivial — pero solo funcionaría con la aplicación enfocada, y
  para salir de la vista mini haría falta registrar un atajo global del sistema
  (`RegisterHotKey`), con el conflicto de teclas que eso trae. Se puede resolver
  después sin cambiar la spec ni el diseño.
- ¿La vista mini debería mostrar el tiempo total de todos los timers cuando hay
  varios corriendo, en lugar de solo indicar que existen? Requiere ver la
  feature en uso para saber si molesta. Cambiaría un escenario de la spec, no el
  enfoque.
