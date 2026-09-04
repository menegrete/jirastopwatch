## Why

Con un timer corriendo, la ventana principal ocupa espacio de pantalla que no
aporta nada: el usuario ya eligió el issue y solo necesita ver que el reloj
sigue andando y cuánto lleva. Hoy la única alternativa es minimizar —a la
taskbar o a la bandeja, según `MinimizeToTray`— y ahí pierde de vista el timer
por completo, con el riesgo de olvidarse de que está corriendo o de que lo
pausó.

Falta un estado intermedio: esconder la ventana principal y dejar solo el
mínimo indispensable a la vista, siempre encima del resto y fuera del camino.

## What Changes

- Nueva **vista mini**: una ventana flotante chica que muestra el issue key, el
  summary abreviado y el tiempo transcurrido del timer activo, con botones para
  pausar/reanudar y para volver a la ventana completa.
- La vista mini se activa con un botón nuevo en la barra superior de la ventana
  principal. Al activarse, la ventana principal se esconde; al volver, se
  restaura en la posición y tamaño que tenía.
- La vista mini queda **siempre encima** de otras ventanas y se puede arrastrar
  libremente. Al soltarla cerca de un borde de la pantalla se **pega** a ese
  borde. Su posición persiste entre ejecuciones.
- El tiempo mostrado se actualiza **cada segundo**. Hoy el ciclo de refresco de
  la aplicación es de 30 segundos porque está atado al polling de Jira; se
  separa el refresco de UI del refresco de red.
- Se introduce un modelo de estado observable del timer activo
  (`ActiveTimerViewModel`), único origen de la información que consume la vista
  mini. Reemplaza el recorrido ad-hoc de `issueControls` buscando el timer
  corriendo.
- La vista mini se implementa en **WPF** dentro del mismo proyecto
  (`UseWPF` junto a `UseWindowsForms`), no en WinForms. El motivo está en
  `design.md`.

No hay cambios breaking: sin activar la vista mini, la aplicación se comporta
exactamente como hoy.

## Capabilities

### New Capabilities

- `mini-timer-view`: qué información muestra la vista mini, cómo se entra y se
  sale de ella, cómo se posiciona y se pega a los bordes, qué acciones permite,
  qué muestra cuando hay varios timers corriendo o ninguno, y con qué frecuencia
  se actualiza.

### Modified Capabilities

- `ui-theming`: el requerimiento "Superficie consistente en toda la aplicación"
  enumera las ventanas de la aplicación en sus escenarios; hay que incorporar la
  vista mini. Además, la garantía de tema deja de depender de un mecanismo único
  (`ThemeApplier` recorriendo el árbol de `Control`) porque la vista mini no es
  WinForms: el requerimiento pasa a expresarse independientemente del framework
  de UI.

## Impact

**Código afectado**

- `source/StopWatch/StopWatch.csproj`: `UseWPF`, y `NoWarn` para `WPF0001`
  (la API `ThemeMode` de WPF es experimental y el proyecto compila con
  `TreatWarningsAsErrors`).
- `source/StopWatch/UI/MainForm.cs`: botón de activación, esconder/restaurar la
  ventana, y separación del ticker de 30 s (`defaultDelay`) respecto del
  refresco de UI.
- `source/StopWatch/UI/MainForm.Designer.cs`: el botón nuevo en `pTop`.
- `source/StopWatch/Settings/Settings.cs` y `Properties/Settings.settings`:
  persistencia de la posición y del borde al que está pegada la vista mini.
- Archivos nuevos: `UI/MiniTimerWindow.xaml` (+ code-behind) y
  `Model/ActiveTimerViewModel.cs`.

**Sin cambios**

- `WatchTimer`, `JiraClient` y todo lo relativo a worklogs: la vista mini
  observa, no altera la lógica de medición ni de posteo.
- `ThemeApplier` sigue existiendo tal como está para las ventanas WinForms.

**Dependencias**

- Ninguna nueva de NuGet. WPF viene con `net10.0-windows`.

**Riesgo abierto**

- En un proceso WinForms no existe un `System.Windows.Application`, así que
  `Application.Current` es `null`. Queda por verificar que el `ThemeMode` de WPF
  aplicado por ventana cargue los diccionarios Fluent en ese contexto. Es la
  primera tarea del change y condiciona cómo se tematiza la vista mini.
