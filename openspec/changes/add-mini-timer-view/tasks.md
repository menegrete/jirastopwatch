## 1. Spike: WPF dentro del proceso WinForms

Objetivo: despejar el riesgo de `Application.Current == null` antes de escribir
la feature. Si algo de esto falla, revisar D1 y D6 en `design.md` antes de
seguir.

- [x] 1.1 Crear y checkoutear la rama `feature/mini-timer-view` desde `main`
- [x] 1.2 Agregar `<UseWPF>true</UseWPF>` al `PropertyGroup` de `source/StopWatch/StopWatch.csproj` y verificar que la solución siga compilando limpia con `TreatWarningsAsErrors=true`
- [x] 1.3 Agregar `<NoWarn>$(NoWarn);WPF0001</NoWarn>` al mismo `PropertyGroup` (la API `ThemeMode` de WPF es experimental)
- [x] 1.4 Spike descartable: una `Window` WPF mínima abierta desde un handler de `MainForm`, confirmar que se muestra, se renderiza y se cierra sin `System.Windows.Application`
- [x] 1.5 En ese mismo spike, probar `window.ThemeMode` y anotar en `design.md` si carga los diccionarios Fluent en este contexto (dato de entrada para la discusión de migración; no cambia D6)
- [x] 1.6 Probar `AllowsTransparency = true` con `WindowStyle = None` y confirmar que no aparecen artefactos de borde
- [x] 1.7 Borrar el spike

## 2. Modelo del timer activo

Tiene valor propio: reemplaza el recorrido ad-hoc de `issueControls` y es
testeable, incluso si la vista mini se descartara.

- [x] 2.1 Crear `source/StopWatch/Model/ActiveTimerViewModel.cs` implementando `INotifyPropertyChanged`, con `IssueKey`, `Summary`, `Elapsed`, `IsRunning` y `HasOtherRunningTimers`
- [x] 2.2 Implementar la resolución del issue activo según la spec (corriendo → último arrancado → último que corrió → seleccionado en la ventana principal)
- [x] 2.3 Alimentar el ViewModel desde `MainForm`: en `issue_TimerStarted`, en `Issue_TimerReset`, en `Issue_Selected` y al final de `InitializeIssueControls`
- [x] 2.4 Exponer `Refresh()` como el punto de entrada del tick y dejar el ViewModel sin timer propio; el tick de 1 s pasa a la ventana de la vista mini (tarea 4.9). Ver D3 en `design.md`, ajustado durante la implementación
- [x] 2.5 Reescribir `MainForm.HandleSessionLock` para que consulte el ViewModel en lugar de recorrer `issueControls`, conservando el comportamiento de `lastRunningIssue`
- [x] 2.6 Agregar `source/StopWatchTest/ActiveTimerViewModelTest.cs` cubriendo la resolución del issue activo en los cuatro casos y el valor de `HasOtherRunningTimers` con uno y con dos timers corriendo

## 3. Persistencia de la posición

- [x] 3.1 Agregar la propiedad `MiniViewLocation` (`string`, default `""`) en `source/StopWatch/Properties/Settings.settings` / `Settings.Designer.cs`, siguiendo el patrón de las propiedades existentes
- [x] 3.2 Agregar `MiniViewLocation` a `Settings.cs`: propiedad pública, lectura en `ReadSettings()` y escritura en `Save()`
- [x] 3.3 Implementar el parseo de `"x,y"` y la validación contra `Screen.AllScreens`, con fallback a una posición visible de la pantalla principal cuando la guardada no cae en ninguna pantalla

## 4. La ventana de la vista mini

- [x] 4.1 Crear `source/StopWatch/UI/MiniTimerWindow.xaml` y su code-behind: `WindowStyle="None"`, `AllowsTransparency="True"`, `Topmost="True"`, `ShowInTaskbar="False"`, `ResizeMode="NoResize"`, `SizeToContent="Manual"`
- [x] 4.2 Maquetar el contenido: franja de estado, issue key, summary con `TextTrimming="CharacterEllipsis"`, tiempo, botón de pausa/reanudar y botón de volver
- [x] 4.3 Bindear todo a `ActiveTimerViewModel` (nada de asignaciones desde code-behind)
- [x] 4.4 Agregar el indicador de "hay otros timers corriendo", visible según `HasOtherRunningTimers`
- [x] 4.5 Construir los `SolidColorBrush` de la ventana desde `Theme.Current`, con un helper de conversión `System.Drawing.Color` → `System.Windows.Media.Color`; reconstruirlos cuando cambia el tema
- [x] 4.6 Implementar el arrastre con `DragMove()` en `MouseLeftButtonDown` sobre el fondo, sin capturar los clicks de los botones
- [x] 4.7 Implementar el snap: al terminar el arrastre, alinear al borde de `Screen.WorkingArea` de la pantalla bajo el puntero si la distancia es menor al umbral; guardar la posición resultante en settings
- [x] 4.8 Aplicar esquinas redondeadas y sombra
- [x] 4.9 Agregar el `DispatcherTimer` de 1 s que llama a `viewModel.Refresh()`, arrancado al mostrarse la ventana y parado al esconderla
- [x] 4.10 Llamar a `ElementHost.EnableModelessKeyboardInterop(window)` después de `Show()` para que la ventana reciba teclado dentro de la bomba de mensajes de WinForms (hallazgo del spike, ver Risks en `design.md`)

## 5. Integración con la ventana principal

- [x] 5.1 Agregar el botón de activación a `pBottom` en `MainForm.Designer.cs`, al lado de `pbSettings`, con su tooltip (la tarea decía `pTop`; `pbSettings` en realidad vive en `pBottom`)
- [x] 5.2 Agregar el icono correspondiente a `Properties/Resources` y a `ThemeIcons`, en las variantes de ambos temas, siguiendo el patrón de `ThemeIcons.Settings`
- [x] 5.3 Implementar el handler: guardar `Location`, `Size` y `WindowState`, instanciar la ventana WPF de forma diferida (la primera vez), mostrarla y hacer `Hide()` de `MainForm`
- [x] 5.4 Implementar el retorno: esconder la vista mini, `Show()` de `MainForm` y restaurar posición, tamaño y estado guardados
- [x] 5.5 Verificar que la vista mini no interfiera con `MinimizeToTray` ni con `notifyIcon`: nunca deben quedar visibles la vista mini y el icono de bandeja al mismo tiempo
- [x] 5.6 Asegurar que al cerrar la aplicación desde la vista mini se ejecute `SaveSettingsAndIssueStates()`
- [x] 5.7 Confirmar que la aplicación siempre arranca en la ventana principal, sin persistir que estaba en vista mini

## 6. Verificación manual

Un ítem por escenario de `specs/mini-timer-view/spec.md` que no quede cubierto
por los tests de la tarea 2.6.

- [x] 6.1 Con un timer corriendo, activar la vista mini: se muestran key, summary y tiempo; la ventana principal desaparece de la barra de tareas; el timer sigue corriendo
- [x] 6.2 Observar 5 segundos: el tiempo avanza segundo a segundo. Pausar: el tiempo deja de cambiar
- [x] 6.3 Pausar y reanudar desde la vista mini; volver a la ventana principal y verificar que el issue quedó en ese estado con el tiempo correcto
- [x] 6.4 Con `AllowMultipleTimers` activado y dos timers corriendo, verificar que se muestra el último arrancado y aparece el indicador; pausar uno y ver que el indicador desaparece
- [x] 6.5 Volver a la ventana completa: reaparece en la misma posición y tamaño que tenía
- [x] 6.6 Maximizar otra aplicación: la vista mini sigue encima. Recorrer las ventanas del sistema: no figura entre ellas
- [x] 6.7 Arrastrar al medio de la pantalla: queda donde se la soltó. Soltar cerca de un borde: se pega y no queda tapada por la barra de tareas
- [x] 6.8 Arrastrar empezando sobre el botón de pausa: la ventana no se mueve y ningún timer cambia de estado
- [x] 6.9 Cerrar y reabrir la aplicación: la vista mini reaparece en la posición guardada
- [x] 6.10 Con la posición guardada correspondiente a un monitor desconectado, verificar que aparece en la pantalla principal — cubierto por `ScreenPlacementTest`, que pasa las pantallas como argumento y no necesita desenchufar un monitor
- [x] 6.11 Activar la vista mini con tema oscuro y con tema claro; cambiar de tema y volver a activarla: los colores acompañan en ambos casos
- [x] 6.12 Activar la vista mini sin haber arrancado ningún timer: muestra el issue seleccionado en cero — verificado renderizando la ventana real a PNG (`0:00`, glifo de play, issue seleccionado)
- [x] 6.13 Con un summary largo: se trunca, y key y tiempo quedan completos — verificado renderizando la ventana real a PNG

## 7. Cierre

- [x] 7.0 Borrar el harness de preview temporal (`UI/MiniPreview.cs`) y su hook en `Program.cs`
- [x] 7.1 Compilar en Release y confirmar cero warnings con `TreatWarningsAsErrors=true`
- [x] 7.2 Correr `dotnet test` y verificar que pasa toda la suite
- [x] 7.3 Anotar en `design.md`, bajo "Futuro", el resultado del spike 1.5 y la impresión de trabajar en WPF en este proyecto — es la entrada de la decisión sobre migrar las ventanas existentes
- [ ] 7.4 Abrir el PR contra `main`
