## Context

Ver `proposal.md` — Why. Lo que sigue son las restricciones del código actual
que condicionan el enfoque.

- `IssueListViewModel.Move(issue, newIndex)`
  ([Model/IssueListViewModel.cs:237](../../../source/StopWatch/Model/IssueListViewModel.cs))
  ya existe, mueve la fila dentro de `Issues` (un `ObservableCollection`) y
  reselecciona la fila movida. No tiene test propio hoy. No hace nada si
  `newIndex` queda fuera de rango — no hace falta clampear antes de
  llamarlo.
- `IssueListViewModel.Issues_CollectionChanged`
  ([Model/IssueListViewModel.cs:373](../../../source/StopWatch/Model/IssueListViewModel.cs))
  ya se dispara en cada `Add`, `Remove` y `Move` sobre `Issues` (es el
  handler de `CollectionChanged` del `ObservableCollection`), y hoy
  recalcula `CanAdd`/`CanRemove`. Es el único punto que necesita el nuevo
  cálculo de `IsFirst`/`IsLast`.
- `IssueViewModel.IsCurrent`
  ([Model/IssueViewModel.cs:165](../../../source/StopWatch/Model/IssueViewModel.cs))
  es el precedente directo para `IsFirst`/`IsLast`: un booleano con backing
  field, seteado con el helper `Set(ref field, value, "Nombre")` que ya
  dispara `PropertyChanged`. `IssueListViewModel.SetCurrent` recorre
  `Issues` y setea `IsCurrent` fila por fila — mismo patrón de recorrido
  completo que usaría el nuevo cálculo.
- La fila de `MainWindow.xaml` (compact y spacious, dos `DataTemplate`
  separados) no tiene columnas libres: las 9 columnas del grid son todas
  `Auto` o fijas, sin lugar para agregar controles sin ensanchar la fila.
- El precedente de "ícono que aparece solo con hover" ya existe:
  `btnCopyKey`/`btnCopyParentKey`
  ([UI/MainWindow.xaml:99-124](../../../source/StopWatch/UI/MainWindow.xaml))
  están `Visibility="Collapsed"` por default, superpuestos (`Grid.Column`
  compartida con otro control, `HorizontalAlignment` + `Margin` para no
  pisarlo) sobre la columna del key, y un `Trigger` en `IsMouseOver` del
  `Border` raíz de la fila (`x:Name="row"`) los hace visibles. Los triggers
  de estado del propio dato (`CanOpen`, `HasParent`) van después en
  `DataTemplate.Triggers`, porque el orden de triggers importa: el último
  que aplica gana.
- No hay infraestructura de drag&drop en el proyecto (ni `AllowDrop` en
  ningún lado, ni paquete NuGet para eso) — confirma por qué esa vía quedó
  fuera del alcance (ver proposal.md).
- `StopWatchCommands.cs` ya tiene doce `RoutedUICommand` con el mismo
  helper `Make(text, name, key, modifiers)`; `Ctrl+Up`/`Ctrl+Down` están
  tomados por `SelectPrevious`/`SelectNext`.

## Goals / Non-Goals

**Goals:**

- Exponer `Move` desde dos vías: atajo de teclado sobre la fila
  seleccionada, y botones flotantes por fila que no requieren selección
  previa.
- Que el botón que no aplica en un borde de la lista (subir en la primera,
  bajar en la última) no sea visible, no solo esté deshabilitado.
- No agregar columnas al grid de ninguno de los dos `DataTemplate` de fila.

**Non-Goals:**

- Drag&drop (ver proposal.md — quedó fuera de alcance).
- Reordenar desde la vista mini o el taskbar widget: ninguna de las dos
  vistas lista más de una fila seleccionable de la misma manera que la
  ventana principal, y el issue no las menciona.
- Cualquier límite o validación nueva sobre el orden en sí (por ejemplo,
  agrupar por estado) — es un reordenamiento libre, punto a punto.

## Decisions

### D1: `IsFirst`/`IsLast` como propiedades de `IssueViewModel`, recalculadas en `Issues_CollectionChanged`

Se agregan dos booleanos a `IssueViewModel`, mismo patrón que `IsCurrent`.
`IssueListViewModel.Issues_CollectionChanged` recorre `Issues` y setea
`Issues[0].IsFirst = true` (el resto `false`) e
`Issues[Issues.Count - 1].IsLast = true` (el resto `false`), simétrico a
como `SetCurrent` ya recorre toda la lista para `IsCurrent`.

Alternativa descartada: calcular la posición en XAML con
`AlternationIndex`/`ItemContainerGenerator.IndexFromContainer` sobre el
`ItemsControl`. Es más plomería (converters, `AlternationCount` atado al
tamaño de la lista) para el mismo resultado, y deja la regla de negocio
("qué fila es la primera/última") en la vista en lugar del ViewModel, que
es justamente lo que esta capa existe para evitar (ver CLAUDE.md — Model/
ViewModel layer es UI-framework-agnostic).

### D2: Botones de mover superpuestos sobre el borde derecho de la columna del summary, mismo mecanismo de hover que `btnCopyKey`

Dos botones (`Style="{StaticResource GlyphButton}"`, reusando
`GlyphTriangleUp`/`GlyphTriangleDown` — ya existen en `Styles.xaml` para el
stepper de `SettingsWindow`, mismo significado "subir/bajar") en
`Grid.Column="3"` (la columna del summary, `Width="*"`), superpuestos con
`HorizontalAlignment="Right"`, uno al lado del otro (no apilados
verticalmente - la primera versión los apilaba en 16x14/18x16px y quedaban
demasiado chicos para clickear con precisión). Mismo tamaño que
`btnCopyKey` en cada densidad (18x18 compacto, 20x20 espacioso) y mismo
patrón de márgenes que `btnCopyParentKey`/`btnCopyKey` para ubicarlos uno
junto al otro (`Margin` del de la izquierda = ancho del ícono + separación
+ margen derecho del de la derecha), `Visibility="Collapsed"` por default.
Mismo `Trigger SourceName="row" Property="IsMouseOver"` ya existente los
hace visibles, agregando dos `Setter` más ahí. La visibilidad por borde se
agrega como dos `DataTrigger` adicionales (`Binding="{Binding IsFirst}"`/
`IsLast`, `Value="True"` → `Collapsed`), después del trigger de hover en la
lista de `DataTemplate.Triggers` — mismo orden que ya usan `CanOpen`/
`HasParent` para ganarle al hover.

**Corrección sobre la propuesta inicial**: se había pensado en
`Grid.Column="4"` (la columna del start/stop) para que quedaran "a la
izquierda del play/pause". Esa columna es `Width="Auto"`, medida
exactamente al tamaño del botón de play — no tiene margen ocioso como sí
tiene la columna 1 (fija, 109/123px) donde se apoya `btnCopyKey`. Superponer
ahí un control visible haría crecer la columna (y la fila entera) en cada
hover, un reflow que `btnCopyKey` no sufre porque su columna ya es más
ancha que su contenido. La columna 3 es `*` (ocupa el espacio sobrante), así
que un overlay en su borde derecho no cambia el ancho de ninguna columna
`Auto` vecina - mismo resultado visual (pegado a la izquierda del botón de
play) sin el efecto secundario.

Alternativa descartada: columna propia para los botones. Requiere
ensanchar las 620px mínimas de la ventana (spec `issue-list` — ancho
mínimo) y el cambio se sentiría fuera de lugar en una fila que ya está
optimizada para no desperdiciar espacio horizontal.

### D3: Dos comandos nuevos en `StopWatchCommands.cs`, `Ctrl+Shift+Up`/`Ctrl+Shift+Down`

`MoveUp`/`MoveDown`, mismo helper `Make(...)`. La elección de modificador
sigue la convención ya visible en la clase: los comandos que actúan sobre
la fila seleccionada son `Ctrl+<algo>`; como `Ctrl+Up`/`Ctrl+Down` ya
seleccionan la fila anterior/siguiente, sumar `Shift` es la extensión
natural ("lo mismo, pero moviendo en vez de seleccionando") y no choca con
ningún atajo existente.

### D4: Los botones actúan sobre la fila bajo el mouse, no sobre la fila seleccionada

Mueven la fila que los muestra (la que tiene el hover), igual que
`btnCopyKey`/`btnRemove`/`btnReset` ya actúan sobre su propia fila sin
requerir seleccionarla primero. El atajo de teclado, en cambio, actúa sobre
`issues.Current` (la fila seleccionada), igual que el resto de
`StopWatchCommands`. Son dos maneras de llegar al mismo `Move`, cada una
consistente con el resto de su propia categoría de control.

## Risks / Trade-offs

- [Los botones flotantes, al superponerse sobre la columna del start/stop,
  podrían quedar visualmente apretados en densidad compacta, donde esa
  columna es más chica] → Mitigación: usar el mismo tamaño de ícono
  (18x18) que ya usan `btnCopyKey`/`btnCopyParentKey` en esa densidad, en
  vez del 24x24 de los botones de acción de fila.
- [Con hover activo sobre una fila que deja de ser la primera/última por un
  movimiento de otra fila, el `Trigger` de `IsMouseOver` no se reevalúa
  solo — depende de que `IsFirst`/`IsLast` cambien] → No es un riesgo real:
  el binding a `IsFirst`/`IsLast` dispara `PropertyChanged`, y WPF
  reevalúa el `DataTrigger` correspondiente sin necesidad de un nuevo
  evento de mouse. Ya cubierto por el escenario "Una fila que deja de ser
  la primera o la última" en el spec.

## Migration Plan

No aplica migración de datos: el orden ya persiste hoy vía
`IssueListViewModel.Persist()`, que guarda `Issues` en el orden en que
están al momento de guardar. No hay cambio de esquema en
`Settings`/`PersistedIssue`.
