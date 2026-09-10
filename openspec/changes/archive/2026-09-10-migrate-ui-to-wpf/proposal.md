## Why

La ventana principal y sus diálogos están construidos en WinForms, una
tecnología que no sabe tematizarse ni acomodar contenido de tamaño variable.
Eso puso un techo a lo que la aplicación puede presentar: cada fila de issue
mide exactamente lo mismo, el summary se trunca aunque haya lugar, el alto de
la ventana es `IssueCount × altoFijo`, y no hay forma de transicionar un
cambio de estado — un timer arranca y el color salta.

El techo no es hipotético: sostener el tema oscuro sobre WinForms ya costó un
recorrido del árbol de controles que redibuja checkboxes a mano y un puñado de
llamadas nativas no documentadas. Cada capacidad visual nueva paga ese peaje
otra vez.

La vista mini ya demostró en producción que WPF convive con el proceso
WinForms y que el tema de la aplicación se puede expresar como recursos WPF.
Este cambio extiende esa decisión al resto de la interfaz.

## What Changes

- La lista de issues pasa a ser una colección observable renderizada por
  plantilla, en lugar de N controles instanciados y posicionados a mano.
- Las filas de issue admiten alto variable: el summary ocupa una o dos líneas
  según entre o no en el ancho disponible.
- El usuario puede elegir entre una presentación densa y una espaciada, y la
  elección persiste entre ejecuciones.
- El ancho de la ventana principal pasa a ser ajustable por el usuario y
  persiste. El alto sigue derivándose de las filas — ahora como suma de sus
  altos individuales en lugar de un producto — y se sigue limitando al área de
  trabajo de la pantalla.
- Los cambios de estado visibles (arrancar y pausar un timer, seleccionar una
  fila) se presentan con una transición en lugar de un salto de color.
- Los diálogos de settings, worklog, edición de tiempo y about se reconstruyen
  con la misma apariencia y el mismo comportamiento que tienen hoy.
- La lógica de negocio que hoy vive dentro de los controles de UI —postear
  worklog y comentario, consultar summary y estimate restante, resolver el
  filtro activo— se extrae a un modelo con tests antes de tocar la
  presentación.
- Se elimina el código que existe únicamente para tematizar WinForms.
- **BREAKING** (interno): la aplicación deja de ser una aplicación WinForms con
  una ventana WPF y pasa a ser una aplicación WPF que sigue referenciando
  WinForms para el ícono de bandeja y la enumeración de pantallas.

## Capabilities

### New Capabilities
- `issue-list`: cómo se presenta la lista de issues en la ventana principal —
  cuántas filas hay, qué alto tienen, cómo se ajusta el summary al ancho
  disponible, qué elige el usuario sobre densidad y ancho, y cómo se deriva de
  todo eso el tamaño de la ventana.

### Modified Capabilities
- `ui-theming`: los cambios de estado que hoy se comunican por un salto de
  color pasan a comunicarse con una transición animada; hace falta fijar que la
  transición no puede ser el único portador del estado, que no puede degradar
  la legibilidad y que su color de llegada pertenece al tema activo.

## Impact

- `source/StopWatch/UI/`: se reescribe por completo. `MainForm`,
  `IssueControl`, `SettingsForm`, `WorklogForm`, `EditTimeForm` y `AboutForm`
  se reemplazan por ventanas WPF. `ThemeApplier` y `ModalDialog` se eliminan.
- `source/StopWatch/Program.cs`: el arranque pasa a `App.xaml`. La instancia
  única por mensaje de ventana se re-implementa sobre el handle de la ventana
  WPF.
- `source/StopWatch/Helpers/NativeMethods.cs`: se eliminan las llamadas de modo
  oscuro para WinForms; sobrevive el mensaje de instancia única.
- `source/StopWatch/Settings/Settings.cs`: settings nuevos para densidad y
  ancho de la ventana principal.
- `source/StopWatch/Model/`: recibe el modelo de issue extraído en la primera
  fase.
- `source/StopWatch/StopWatch.csproj`: sigue con WinForms y WPF habilitados a
  la vez, deliberadamente y de forma permanente.
- `source/StopWatchTest/`: la suite existente no se modifica y debe seguir
  pasando durante toda la migración; se agregan tests sobre la lógica extraída.
- Sin impacto en `Jira/`, en la persistencia de configuración ni en el formato
  de los datos guardados.
