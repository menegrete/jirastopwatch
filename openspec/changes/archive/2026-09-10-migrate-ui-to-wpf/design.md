## Context

Ver `proposal.md` — Why para la motivación, y los specs de `issue-list` y
`ui-theming` para el comportamiento exigido.

El estado del que se parte, en lo que condiciona el diseño:

- El proyecto ya compila con `UseWindowsForms` y `UseWPF` a la vez, y ya tiene
  una ventana WPF en producción (`MiniTimerWindow`) con su puente de tema
  (`ThemeBrushes`). El camino está probado.
- La lógica de negocio de la aplicación vive fuera de la UI —`Jira/`, `Model/`,
  `Settings/`, `Helpers/`— y tiene tests. **Salvo** cuatro métodos de
  `IssueControl` que llaman a Jira leyendo su entrada desde los propios
  controles: `UpdateSummary`, `UpdateRemainingEstimate`, `PostAndReset` y
  `LoadIssues`. Ninguno tiene tests.
- Esos cuatro comparten un patrón: `Task.Factory.StartNew` para salir del hilo
  de UI, y `InvokeIfRequired` para volver a leer o escribir un control. El
  estado vive en los controles, no en un modelo.
- `LoadIssues` además resuelve el filtro activo buscando un ComboBox por nombre
  en el árbol de controles de la ventana principal
  (`Application.OpenForms[0]`), con un TODO de los autores originales pidiendo
  una capa de controlador.
- `MainForm.InitializeIssueControls` instancia y posiciona las filas a mano
  (`issue.Top = i * issue.Height`) y fija `ClientSize` como
  `IssueCount × altoFijo`. La ventana es `FixedSingle`, sin maximizar.
- `ThemeApplier` (579 líneas) recorre el árbol de controles y dibuja glifos de
  checkbox a mano; `NativeMethods` tiene un bloque de llamadas de modo oscuro
  —incluida una por ordinal a `uxtheme`— que existe sólo para WinForms.

## Goals / Non-Goals

**Goals:**

- Que la fase de extracción quede verificable por sí sola: al terminarla, la
  aplicación sigue siendo WinForms, se comporta igual, y la suite de tests pasa
  con tests nuevos sobre la lógica extraída.
- Que la fase de migración no tenga que decidir nada sobre reglas de negocio:
  cuando empieza, lo único que queda es dibujar un modelo que ya existe.
- Que el resultado se vea como la misma aplicación que hoy, salvo en lo que los
  specs cambian deliberadamente.

**Non-Goals:**

- Dejar de referenciar WinForms. El ícono de bandeja y la enumeración de
  pantallas se siguen resolviendo con WinForms, de forma permanente y
  deliberada.
- MVVM en los diálogos. Sólo la lista de issues justifica bindings.
- Adoptar el tema Fluent de WPF. El tema de la aplicación se sigue expresando
  con `Theme` y `ThemeBrushes`, como ya hace la vista mini.
- Cambiar el formato de la configuración persistida, más allá de agregar
  claves nuevas.

## Decisions

### D1. Dos fases en un solo change, con un corte verificable entre ellas

La fase 1 extrae el modelo sin tocar la presentación; la fase 2 reemplaza la
presentación. El corte entre fases es un punto donde la aplicación compila,
corre y pasa los tests.

*Alternativa considerada:* dos changes separados. Se descartó a pedido: el
valor de la extracción es habilitar la migración, y separarlos deja un change
archivado cuyo único propósito es otro que todavía no existe. El corte
verificable se conserva igual, como frontera dentro de `tasks.md`.

*Alternativa considerada:* migrar directo a XAML sin extraer. Se descartó: las
reglas de worklog y estimate se reescribirían sin ningún test que las cubra.

### D2. El estado de un issue se muda a un modelo observable

Se introduce un modelo por issue que expone key, summary, comentario, método y
valor de estimate, su `WatchTimer` y si es el issue actual, y que notifica
cambios. La lista de la ventana principal pasa a ser una colección observable
de esos modelos.

Esto es lo que habilita al mismo tiempo la plantilla de fila, el alto variable
y la persistencia: hoy `MainForm_Shown` hidrata los controles desde
`Settings.PersistedIssues` y `SaveSettingsAndIssueStates` los vuelve a leer;
después, hidrata y lee la colección.

*Alternativa considerada:* dejar el estado en los controles WPF y hacer
code-behind también acá. Se descartó: sin colección observable no hay plantilla
de fila, y sin plantilla de fila no hay alto variable — que es la capacidad
pedida.

### D3. La orquestación contra Jira sale de la fila a un servicio

`PostAndReset` (orden comentario → worklog → reset, y las reglas de
`WorklogCommentSetting`), la consulta de summary y la de estimate restante
pasan a un servicio que recibe sus datos por parámetro y devuelve resultados.
No conoce controles ni ventanas.

El filtro activo deja de resolverse buscando un ComboBox por nombre: se pasa
como dependencia. Eso cierra el TODO de `IssueControl.LoadIssues`.

Los tests nuevos de la fase 1 apuntan acá: es la lógica que hoy no tiene
ninguno y la que más caro sale equivocarse.

### D4. `Task.Factory.StartNew` + `InvokeIfRequired` se reemplaza por `async/await`

WPF instala un `SynchronizationContext` que devuelve la continuación al hilo de
UI sola. `InvokeExtensions` deja de tener razón de ser en el código migrado.

La conversión se hace en la fase 1, todavía bajo WinForms, donde el mismo
mecanismo también funciona. Así la fase 2 no mezcla el cambio de modelo de
concurrencia con el cambio de tecnología de UI.

### D5. El alto de la ventana se deriva; el ancho lo elige el usuario

`SizeToContent` vertical: el alto lo calcula el layout como suma de los altos
de fila, en lugar del producto `IssueCount × altoFijo` de hoy. El ancho pasa a
ser ajustable, con mínimo, y persiste.

Esto preserva el invariante actual —el alto es función de las filas— mientras
habilita las tres capacidades que dependen de él: densidad conmutable, summary
en dos líneas y alto de fila variable. El ancho es la perilla que decide si un
summary entra en una línea o en dos, y por eso es el eje que tiene sentido
dejar en manos del usuario.

El clamp al área de trabajo ya existe hoy (`MainForm.cs:521-526`) y se
conserva; el recorrido de la lista cuando no entra es el desborde de ese clamp,
no el modo normal de operación.

*Alternativa considerada:* redimensionado en ambos ejes con scroll siempre
presente. Se descartó por decisión del usuario: rompe el invariante.

### D6. La densidad son dos plantillas de fila, no un cálculo

Compacta y espaciada se expresan como dos plantillas que difieren en espaciado
y tipografía, elegidas por la densidad activa. Evita repartir factores de
escala por todo el XAML y deja lugar a que las dos presentaciones difieran en
más que el alto si hiciera falta.

### D7. Las transiciones son animaciones sobre el color, con el estado también
en otra parte

El spec exige que la transición no sea el único portador del estado. El estado
sigue estando en el modelo y, en la presentación, en algo que no dependa de
dónde esté la animación en su curso.

El escenario de dos selecciones rápidas obliga a que una animación nueva
reemplace a la anterior en lugar de encolarse, y el de cambio de tema, a que el
color de llegada se resuelva contra el tema vigente al terminar y no al
empezar.

### D8. La instancia única se re-implementa sobre el handle de la ventana WPF

`WM_SHOWME` se sigue registrando y transmitiendo igual; lo que cambia es dónde
se lo escucha: un hook sobre el handle de la ventana principal en lugar de
`WndProc` del formulario. El mutex y el `PostMessage` del arranque no cambian.

### D9. Se elimina el andamiaje de tema de WinForms

`ThemeApplier`, `ModalDialog` y las llamadas de modo oscuro de `NativeMethods`
se borran cuando desaparece la última ventana WinForms, no antes. Sobreviven
`Theme`, `ThemeBrushes` y, de `NativeMethods`, el mensaje de instancia única.

Los diálogos modales pasan a `Owner` + `Topmost`, que es lo que `ModalDialog`
emulaba a mano.

## Risks / Trade-offs

- **La fase 2 reescribe `IssueControl` sin tests de UI que la cubran** → la
  fase 1 saca de ahí todo lo que no es presentación y lo cubre con tests; lo
  que queda para reescribir a ojo es disposición y enlaces.
- **Los 13 atajos de teclado de `ProcessCmdKey` se re-implementan de cero** →
  se listan uno por uno en `tasks.md` y se verifican contra el código actual
  antes de borrarlo, en lugar de reconstruirlos de memoria.
- **El comportamiento de la bandeja depende de que la ventana esté oculta, y
  hoy se dispara desde `MainForm_Resize`** → WPF no emite ese evento igual; el
  disparador pasa a ser el estado de la ventana, y hay que verificarlo junto
  con la vista mini, que ya interactúa con esa lógica.
- **`SizeToContent` en WPF puede entrar en ciclo con contenido que depende del
  ancho** → el ancho es fijo durante un ciclo de layout (lo fija el usuario, no
  el contenido), así que la dependencia es en un solo sentido: ancho → alto.
- **DPI: WinForms y WPF no declaran el awareness igual** → hoy ya conviven, así
  que el riesgo es de regresión, no nuevo; se verifica en pantallas con escala
  distinta antes de cerrar la fase 2.
- **Ancho persistido y vista mini comparten la lógica de "que caiga en una
  pantalla existente"** → se reutiliza `ScreenPlacement`, que ya tiene tests,
  en lugar de escribir una segunda validación.
- **El change es grande y no hay punto de rollback parcial dentro de la fase
  2** → la frontera entre fases es el punto de rollback; la fase 2 se hace
  entera o se revierte entera.
