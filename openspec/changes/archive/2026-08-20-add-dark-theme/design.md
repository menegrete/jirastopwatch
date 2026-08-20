## Context

Ver `proposal.md` — Why para la motivación. Lo que importa a nivel de diseño son
tres restricciones de la plataforma:

1. **WinForms sobre .NET Framework 4.8 no tiene theming.** No hay
   `Application.SetColorMode` (llegó en .NET 9), `SystemColors` no se puede
   sobrescribir, y los controles con visual styles activos ignoran `BackColor`.
2. **Los colores se asignan de forma imperativa y dispersa.** Hay ~20 sitios que
   escriben color en runtime en respuesta a eventos (validación, timer, conexión,
   selección de issue). Un aplicador de una sola pasada al arranque sería
   sobrescrito por el primer cambio de estado.
3. **Los `IssueControl` se crean dinámicamente.** `InitializeIssueControls()` se
   invoca desde cuatro lugares distintos en `MainForm`, incluido después de
   cambiar settings. La cantidad de filas depende de la configuración.

Inventario relevante, verificado sobre la app corriendo: 5 forms, ~60 controles,
2 scrollbars nativos, 4 separadores `BorderStyle.Fixed3D`, 9 botones con
`UseVisualStyleBackColor = true`, 10 CheckBox/RadioButton, 4 ComboBox (uno ya en
`OwnerDrawVariable`), 2 DateTimePicker con `ShowUpDown = true`.

## Goals / Non-Goals

**Goals:**

- Un único lugar donde vive la definición de colores, para que agregar o ajustar
  una paleta no requiera tocar los forms.
- Que un cambio de estado en runtime no pueda producir un color fuera del tema.
- Degradación limpia donde la plataforma no colabora: si un truco de API no
  documentada no está disponible, el elemento queda claro pero nada se rompe.

**Non-Goals:**

- No se abstrae el theming a un framework reutilizable ni se busca soportar temas
  definidos por el usuario. Dos paletas fijas.
- No se reemplazan controles nativos por reimplementaciones propias salvo donde no
  quede alternativa.
- No se toca el layout. Los cambios de tamaño se limitan a lo que el formato de
  fecha exija.

## Decisions

### D1: Objeto de tema + aplicador recursivo, con owner-draw selectivo

**Elegido:** una clase con la paleta semántica y un aplicador que recorre el árbol
de controles haciendo dispatch por tipo. Encima de eso, owner-draw sólo para los
dos casos que no se pueden resolver con propiedades: glifos de CheckBox/RadioButton
y chrome de ComboBox.

**Alternativas consideradas:**

- *Dark hardcodeado en los `.Designer.cs`.* Descartado. No ahorra el trabajo caro:
  igual hay que tocar los ~20 sitios de runtime, y una vez ahí, meter la
  indirección sale gratis. Además deja los `.Designer.cs` llenos de literales que
  el diseñador de Visual Studio puede sobrescribir.
- *Capa completa de controles custom (`DarkButton`, `DarkComboBox`, …).* Descartado
  como enfoque general: obliga a cambiar tipos en todos los `.Designer.cs` y a
  reimplementar comportamiento que hoy funciona. Se usa sólo donde hace falta.

**Consecuencia:** los `.Designer.cs` dejan de ser la fuente de verdad de los
colores. Lo que quede ahí es irrelevante en runtime.

### D2: El aplicador corre después de `InitializeComponent`, y por cada `IssueControl` al crearse

Dado que `InitializeIssueControls()` se invoca desde cuatro puntos, aplicar el tema
sólo en el constructor de `MainForm` dejaría sin themear las filas recreadas al
cambiar settings. El aplicador se invoca en el constructor de cada form y, además,
sobre cada `IssueControl` recién instanciado antes de agregarlo a `pMain`.

**Alternativa considerada:** enganchar `ControlAdded` en `pMain` para themear
automáticamente. Descartado por ahora: menos explícito y más difícil de seguir que
una llamada en el sitio de creación.

**Restricción dura: el aplicador no puede tocar `Control.Handle`.**

Correr desde el constructor tiene una consecuencia que no es obvia. Leer
`Control.Handle` fuerza la creación de la ventana en ese momento, fuera del camino
de `Show`/`ShowDialog`. Un diálogo modal creado así nunca toma su owner: queda como
ventana top-level suelta, se va **detrás** de la principal y sigue bloqueando el
input. La aplicación parece colgada y hay que matarla.

Esto se verificó en la práctica: `WorklogForm` desaparecía por completo. Por eso
todo el trabajo a nivel HWND —barra de título, scrollbars, chrome del ComboBox— se
difiere con un helper que actúa si el handle ya existe, y si no se suscribe a
`HandleCreated`. Verificado midiendo orden Z y owner: el diálogo queda primero y
con el owner correcto.

**Segunda trampa relacionada: `Graphics.Clear(Color.Transparent)` pinta negro.**
Los controles que la aplicación dibuja a mano no pueden heredar un `BackColor`
transparente de su contenedor; hay que resolver hacia arriba hasta el primer
ancestro opaco. Se manifestó como rectángulos negros detrás de los radios dentro
del `GroupBox`, que es el único contenedor transparente de la aplicación.

### D3: Los colores de estado se leen del tema, no de literales

Los sitios que hoy escriben `Color.Tomato`, `Color.PaleGreen`, `Color.DarkGreen`,
`SystemColors.Window`, `SystemColors.Control` y `GradientInactiveCaption` pasan a
leer del tema activo. Esto es lo que hace que el requisito "el tema sobrevive a los
cambios de estado" se cumpla por construcción y no por disciplina.

Nota de mapeo: `PaleGreen` es un color de **fondo claro**. En oscuro el rol se
invierte — fondo oscuro con texto claro — no se puede simplemente oscurecer el
valor.

### D4: Paleta

| Token | Valor | Reemplaza a | Dónde |
|---|---|---|---|
| `Background` | `#1E1E1E` | `SystemColors.Window` | forms, `pMain` |
| `Surface` | `#252526` | — | filas de issue, campos |
| `SurfaceActive` | `#094771` | `GradientInactiveCaption` | issue seleccionado |
| `SurfaceDisabled` | `#2D2D30` | `SystemColors.Control` | campos deshabilitados |
| `Border` | `#3F3F46` | `BorderStyle.Fixed3D` | separadores, bordes de campo |
| `Text` | `#E8E8E8` | `ControlText` | texto principal |
| `TextMuted` | `#9A9A9A` | `GrayText` | labels secundarios |
| `Accent` | `#427AA9` | `SteelBlue` | banda superior |
| `AccentText` | `#FFFFFF` | `Color.White` | label de filtro sobre la banda |
| `Link` | `#4FC1FF` | azul default de LinkLabel | LinkLabels |
| `Success` | `#4EC9B0` | `Color.DarkGreen` | estado conectado |
| `Danger` | `#F48771` | `Color.Tomato` (como texto) | estado de error |
| `DangerSurface` | `#5A2A24` | `Color.Tomato` (como fondo) | campo con validación fallida |
| `TimerRunning` | `#2D4A2D` | `Color.PaleGreen` | campo de timer activo |

Además `ButtonHover` (`#3E3E42`) y `ButtonPressed` (`#4A4A4F`), necesarios porque
los botones pierden los visual styles al pasar a `FlatStyle.Flat`.

**Tres ajustes que surgieron al verificar el contraste:**

1. `Danger` se usa hoy en dos roles incompatibles: fondo en los campos de validación
   (`tbTime.BackColor = Tomato`) y texto en el estado de conexión
   (`lblConnectionStatus.ForeColor = Tomato`). Un solo token no puede cubrir ambos,
   así que se separa en `Danger` (texto) y `DangerSurface` (fondo).
2. `AccentText` se hace token explícito en lugar de un `Color.White` literal.
3. `Accent` **sí cambia**: blanco sobre `SteelBlue` (`#4682B4`) da 4.11:1, por debajo
   del mínimo de 4.5:1 que exige el spec. Es una combinación que la aplicación ya
   usa hoy, o sea que el contraste ya estaba mal antes de este change. Se oscurece
   un 6% a `#427AA9`, que da 4.58:1 y es visualmente casi indistinguible.

Los valores están verificados con un cálculo de ratio de contraste sobre los 13
pares texto/fondo que la aplicación puede producir, en ambos temas: 0 fallas.

### D5: Dos paletas y selector persistido en Settings

Se definen ambas paletas y se expone el selector en Settings. En este diseño la
paleta clara es sólo un segundo juego de constantes, así que el costo de código es
marginal; el costo real es el QA de 5 forms × 2 temas, y se asume.

`Settings` gana un campo de tema con el mismo mecanismo que el resto de la
configuración (`Properties.Settings.Default`), con default oscuro para que una
instalación existente que se actualiza arranque en oscuro sin perder el resto de sus
preferencias.

**Reaplicación en caliente:** al confirmar el diálogo de settings hay que reaplicar
el tema a las ventanas abiertas. `MainForm` ya reconstruye las filas de issues al
confirmar settings (`InitializeIssueControls()` se invoca desde ahí), así que las
filas se themean por la vía de D2. Lo que hay que agregar explícitamente es
reaplicar sobre `MainForm` y sus controles fijos.

**Alternativa considerada:** exigir reinicio para cambiar de tema. Descartado: es
una sola llamada al aplicador y evita una fricción innecesaria.

### D6: Los DateTimePicker se conservan; sólo cambia el formato

`ShowUpDown = true` en ambos controles reemplaza el calendario desplegable por
spinners. Eso elimina el elemento realmente intheameable del `DateTimePicker` — el
popup del calendario — y deja sólo el cuerpo del control y las flechas.

Se conserva el control nativo y se le fija `Format = Custom` con
`CustomFormat = "dd/MM/yyyy"`. El campo de hora ya está en `"HH:mm"` y no se toca.
Se ajusta el ancho de `startDatePicker` para que los diez caracteres entren; hoy
sus 115px recortan el formato `Long` heredado por default.

**Alternativa considerada:** reemplazar ambos por campos de texto con máscara.
Descartado: se gana control total del pintado pero se pierde validación, spinners y
accesibilidad que hoy funcionan, a cambio de un beneficio de theming acotado al
cuerpo de dos controles.

**Consecuencia aceptada:** el cuerpo del `DateTimePicker` puede no acompañar
completamente el tema. Es la concesión deliberada de este diseño.

### D7: Elementos del sistema — dos mecanismos distintos

- **Barra de título:** `DwmSetWindowAttribute` con
  `DWMWA_USE_IMMERSIVE_DARK_MODE`. API documentada, atributo 20 en Windows 10
  1809+ y Windows 11 (19 en builds anteriores). Se aplica por ventana.
- **Scrollbars:** `SetWindowTheme(hwnd, "DarkMode_Explorer", null)` de uxtheme.
  **API no documentada.** Aplica a los dos scrollbars nativos: el de `pMain` y el
  del campo de comentario multilínea en el diálogo de worklog.

Ambos van en `Helpers/NativeMethods.cs`, que ya existe para este propósito. Las dos
llamadas se envuelven de forma que un fallo o una versión de Windows sin soporte
deje el elemento claro sin propagar la excepción.

**Alternativa considerada para scrollbars:** un control de scrollbar propio.
Descartado: mucho código y comportamiento sutil (rueda del mouse, teclado,
arrastre) por un elemento secundario.

**Pieza que falta y no es evidente: `SetPreferredAppMode`.** Los nombres de tema
`DarkMode_*` se ignoran en silencio si el proceso no declaró antes su *preferred app
mode*. Esa función se exporta de uxtheme **sólo por ordinal** (135), sin nombre, así
que hay que resolverla a mano con `GetProcAddress` y llamarla antes de crear la
primera ventana. Sin eso, ni el scrollbar ni el ComboBox se oscurecen, y no hay
ningún error que lo indique.

**El scrollbar de `pMain` es casi inalcanzable.** `InitializeIssueControls()`
redimensiona el form para que entren todas las filas, clampeando sólo contra el área
de trabajo de la pantalla. Con el tope de 20 issues eso da unos 649px de alto, por
debajo de cualquier monitor actual, así que la barra no aparece. Se verificó
encogiendo `pMain` a mano: la barra sale en `#171717`, o sea que el mecanismo
funciona; simplemente casi nunca se ve.

### D8: `Fixed3D` es un patrón único con una única solución

Los 4 separadores de Settings y los bordes de los 10 campos de texto usan
`BorderStyle.Fixed3D`, que el sistema dibuja con `ControlDark`/`ControlLight` y no
respeta ningún color asignado. Todos pasan a `BorderStyle.None` con el color de
borde puesto por la aplicación. Se trata como un solo cambio mecánico, no como
catorce casos.

### D9: Un solo icono necesita variante clara

Verificado sobre la app corriendo: play, post-time, carpeta, reset, delete y los
iconos de la banda superior son saturados y se leen bien sobre oscuro. El único
problemático es el engranaje de settings, gris casi negro. Se agrega una variante
clara de ese icono y se selecciona según el tema activo.

**Alternativa considerada:** teñir los iconos en runtime con una matriz de color.
Descartado: resuelve un problema que sólo tiene un caso.

## Risks / Trade-offs

- **Los owner-draw de CheckBox/RadioButton y ComboBox son donde un dark theme se ve
  mal hecho** → son los dos ítems a los que darles tiempo de pulido y revisión
  visual explícita; el resto del cambio es mecánico.
- **`SetWindowTheme` es API no documentada y puede dejar de funcionar** → se
  envuelve de forma que el fallo degrade a scrollbar claro. No hay ruta de código
  que dependa de que funcione.
- **Los botones pierden los visual styles al pasar a `FlatStyle.Flat`** → hay que
  definir a mano los colores de hover y pressed. Sin eso los botones quedan
  visualmente muertos, que es peor que el problema original.
- **El diseñador de Visual Studio puede reintroducir colores en los
  `.Designer.cs`** → el aplicador corre después de `InitializeComponent`, así que
  gana siempre. El riesgo es de confusión al leer el código, no funcional.
- **La paleta clara duplica la superficie de QA** → los owner-draw de los grupos de
  casillas y combos tienen que verificarse en ambos temas, no sólo en oscuro. Es el
  costo asumido en D5.
- **El cuerpo del `DateTimePicker` puede quedar claro** (D6) → concesión
  deliberada; son dos controles en un diálogo secundario.

## Migration Plan

`Properties.Settings.Default` gana un campo de tema. En una instalación existente
toma su valor por default (oscuro) mediante el mecanismo de upgrade que
`Settings.Load()` ya implementa, sin tocar el resto de la configuración del usuario.

Rollback: revertir el cambio. El campo de tema queda huérfano en la configuración
persistida, sin efecto.
