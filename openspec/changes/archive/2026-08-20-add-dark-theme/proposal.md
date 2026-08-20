## Why

JiraStopWatch corre sobre WinForms / .NET Framework 4.8, que no tiene ningún
soporte de dark mode: no existe `Application.SetColorMode` (llegó en .NET 9), no
hay capa de theming y `SystemColors` no se puede sobrescribir. Toda la UI está
cableada a colores claros del sistema, así que en un escritorio en modo oscuro la
app queda como el único panel blanco de la pantalla.

Al inspeccionar la UI corriendo aparecieron además dos defectos preexistentes en
el mismo código que hay que tocar para themear, y conviene arreglarlos acá en vez
de dejarlos para después.

## What Changes

- **Nueva capa de theming.** Un objeto `Theme` con una paleta semántica
  (`Background`, `Surface`, `SurfaceActive`, `Border`, `Text`, `TextMuted`,
  `Accent`, `Link`, `Success`, `Danger`, `TimerRunning`) y un aplicador recursivo
  que la propaga por el árbol de controles de cada form.
- **Los ~20 sitios que asignan color en runtime pasan a leer del theme.** Hoy
  escriben literales (`Color.Tomato`, `Color.PaleGreen`, `Color.DarkGreen`,
  `SystemColors.Window`, `GradientInactiveCaption`) y sobrescribirían cualquier
  aplicación de theme hecha al arranque.
- **Se resuelven los controles que ignoran `BackColor`.** Botones con
  `UseVisualStyleBackColor`, glifos de CheckBox/RadioButton, chrome de ComboBox,
  bordes `BorderStyle.Fixed3D`, scrollbars nativos y la barra de título.
- **Dos paletas y un selector de tema en Settings.** La paleta oscura es el default;
  la clara queda disponible como opción. La selección persiste entre ejecuciones.
- **Formato de fecha/hora del worklog: `dd/MM/yyyy` + `HH:mm` en dos campos.**
  `startDatePicker` hoy no tiene `.Format` seteado, cae en
  `DateTimePickerFormat.Long` y se renderiza clipeado ("Thursday , Augu") dentro
  de sus 115px. Se mantiene la estructura actual de dos controles separados.
- **Fix: `IssueControl.Current` produce `StackOverflowException`.** El getter se
  llama a sí mismo y el setter no guarda el valor. Sobrevive sólo porque nadie lee
  la propiedad. El theming reescribe ese mismo setter.
- **Variante clara del ícono de settings.** El engranaje es gris casi negro y
  desaparece sobre fondo oscuro. Es el único ícono afectado: play, post-time,
  carpeta, reset y delete son saturados y sobreviven.

### Non-goals

- No se rediseña el layout ni la jerarquía visual de ningún form.
- No se migra a .NET moderno para conseguir dark mode nativo.
- No se soportan temas definidos por el usuario. Son dos paletas fijas.
- No se persigue seguir automáticamente el tema del sistema operativo.

## Capabilities

### New Capabilities

- `ui-theming`: cómo se define una paleta, cómo se aplica al árbol de controles de
  cada form, qué color semántico corresponde a cada estado de la UI (timer
  corriendo, issue actual, validación fallida, estado de conexión), y qué se
  espera de los controles nativos que no respetan los colores asignados. Incluye la
  selección de tema por parte del usuario y su persistencia.
- `worklog-start-time`: formato y estructura de los campos de fecha y hora de
  inicio en el diálogo de submit worklog.

### Modified Capabilities

Ninguna. `openspec/specs/` está vacío: este es el primer change del proyecto.

## Impact

**Código afectado**

- `source/StopWatch/UI/` — los 5 forms y sus `.Designer.cs`: `MainForm`,
  `SettingsForm`, `WorklogForm`, `EditTimeForm`, `AboutForm`, más `IssueControl`.
- `source/StopWatch/Helpers/NativeMethods.cs` — se extiende con
  `DwmSetWindowAttribute` (barra de título) y `SetWindowTheme` (scrollbars).
- `source/StopWatch/Settings/Settings.cs` y `Properties/Settings.settings` — campo
  de tema persistido.
- `source/StopWatch/Resources/` e `icons/` — variante clara del engranaje.
- Nuevo: la clase `Theme` y el aplicador.

**Superficie de UI**

5 forms, ~60 controles. Distribución del trabajo: 22 Label, 10 TextBox, 9 Button,
6 CheckBox, 4 RadioButton, 4 ComboBox, 4 PictureBox, 4 LinkLabel, 3 Panel,
2 DateTimePicker, 1 GroupBox, 1 ToolTip, 2 scrollbars nativos.

**Dependencias**

Ninguna nueva. `SetWindowTheme` con `"DarkMode_Explorer"` es API no documentada de
uxtheme y requiere Windows 10 1809+; si no está disponible, degrada a scrollbars
claros sin romper nada.

**Riesgo**

Contenido, no arquitectura: los dos owner-draw (glifo de CheckBox/RadioButton y
chrome de ComboBox) son donde un dark theme mal hecho se nota. El resto del cambio
es mecánico.
