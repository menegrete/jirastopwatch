## Context

Ver proposal.md - Why. Constraints relevantes del código actual:

- La columna de la key en `MiniTimerRowViewModel`'s `TimerRow` (`MiniTimerWindow.xaml`) es `Auto` y el `TextBlock` de la key no tiene ancho fijo - a diferencia de la ventana principal, donde el `TextBox` de la key sí lo tiene y por eso sus botones de copiar se pueden *superponer* sin robarle ancho a nadie. En la vista mini no hay ese margen: si los botones se superponen tapan el propio texto de la key.
- La fila mide `Height="28"` con `GlyphButton` (`Styles.xaml`) definido a 26x26 por defecto; la ventana principal ya pisa ese tamaño a 18x18 para sus botones superpuestos sobre la key.
- `MiniTimerRowViewModel` expone `IssueKey`, `Summary`, `IsRunning`, `ElapsedText` y `Source` (el `ITimerSource` de origen). No expone `CanOpen` ni `HasParent`/`ParentKey` - no hace falta: `CanOpen` en `IssueViewModel` es simplemente `!string.IsNullOrEmpty(Summary)`, dato que `MiniTimerRowViewModel.Summary` ya expone.
- `FlashCopyConfirmation` y la construcción de la URL de "abrir en el navegador" viven hoy como métodos privados de `MainWindow.xaml.cs`, atados a `IssueViewModel`.

## Goals / Non-Goals

**Goals:**
- Copiar el key y abrir en el navegador desde cada fila de la vista mini, con la misma sensación (iconografía, confirmación visual, regla de habilitación) que la ventana principal.
- Que aparecer/ocultar esos controles no reserve espacio permanente: la fila debe verse exactamente igual que hoy cuando el mouse no está sobre la key.
- No tocar `MiniTimerRowViewModel` ni `IssueViewModel`.

**Non-Goals:**
- Copiar el parent key en la vista mini (la vista mini no tiene noción de parent issue; no está en el pedido).
- Ningún control que abra un diálogo (ya excluido por el requirement existente de `mini-timer-view`).
- Rediseñar el layout general de la fila más allá de lo necesario para estos dos controles.

## Decisions

### Contenedor propio para el hover, separado del hover de fila

La fila ya no dispara nada por `row.IsMouseOver` (ese trigger no existe hoy en `MiniTimerWindow.xaml`, a diferencia de `MainWindow.xaml`). Se envuelve el `TextBlock` de la key junto con los dos botones nuevos en un `Grid` con `x:Name="keyArea"`, dentro de la misma columna `Auto` que hoy ocupa solo la key.

Alternativa descartada: reusar el patrón de `MainWindow` (hover de toda la fila). Se descarta porque en la vista mini el usuario puede estar apuntando al botón de pausa/resume de una fila sin querer disparar acciones de copiar/abrir asociadas a la key de esa misma fila; acotar el hover a la key evita ese acoplamiento y es lo que se pidió explícitamente.

### El hover se resuelve en code-behind (MouseEnter/MouseLeave + grace period), no con un `Trigger` de XAML

Primer intento: un `Trigger SourceName="keyArea" Property="IsMouseOver"` puro en XAML, como en `MainWindow`. Falló en la práctica: `keyArea` es un `Grid` sin `Background`, y en WPF un panel sin fondo (ni siquiera `Transparent`) solo es hit-testeable donde hay contenido de un hijo - el espacio entre el texto de la key y donde aparecería un botón no lo es. Al mover el mouse desde la key hacia el botón, `IsMouseOver` se apagaba en ese hueco antes de llegar, colapsando los botones antes de poder hacer click.

Se resuelve con dos cambios juntos:
1. `Background="Transparent"` en `keyArea`, para que todo su rectángulo (incluidos los huecos entre hijos) sea hit-testeable.
2. La visibilidad ya no depende de un `Trigger` sino de handlers `MouseEnter`/`MouseLeave` en el code-behind de `MiniTimerWindow.xaml.cs`: `MouseEnter` muestra los botones y cancela cualquier ocultamiento pendiente; `MouseLeave` no oculta al toque, programa un `DispatcherTimer` de 300ms (constante `HoverGracePeriod`) que oculta si para entonces el mouse no volvió a entrar. Esto además cubre el caso de un movimiento rápido de mouse que WPF coalesce en un solo evento y salta por completo el hueco (nunca dispara `MouseEnter` en el punto intermedio): aunque eso dispare un `MouseLeave` inesperado, el grace period da tiempo a que el siguiente evento (ya sobre el botón, que reactiva `MouseEnter` en `keyArea` por ser hijo suyo) cancele el ocultamiento antes de que se aplique.

Alternativa descartada: sumar solo `Background="Transparent"` sin grace period. Resuelve el caso de movimiento lento (el hueco pasa a ser hit-testeable, así que `IsMouseOver` ya no se apaga ahí), pero no cubre un salto de mouse rápido que aterrice más allá del hueco en un solo evento, salto que sí puede ocurrir en la práctica.

Alternativa descartada: volver al hover de toda la fila (ver decisión anterior). Eliminaría el bug de raíz porque el contenedor de MainWindow ya es hit-testeable (es un `ThemeAnimatedBorder` con `Background` real, no un `Grid` vacío), pero contradice el pedido explícito de acotar el hover a la key.

### Los botones comprimen el summary en vez de superponerse

Como la columna de la key es `Auto` sin ancho fijo, los dos botones (`Collapsed` por defecto, sin ancho) se agregan como hermanos de la key dentro de `keyArea`. Al pasar a `Visible`, `keyArea` crece, y como la columna de `summary` es `*`, se achica automáticamente sin código adicional: es el comportamiento nativo de `Grid` con columnas `Auto`/`*`. Al salir del hover (o de forma incidental, en el próximo refresh tras completar la acción) vuelven a `Collapsed` y el summary recupera su ancho.

Alternativa descartada: superponerlos como en `MainWindow` (mismo ancho de columna, los botones tapan parte del texto). Se descarta porque acá no hay slack de ancho fijo que darles - taparía el propio key en vez de convivir con él, que es exactamente lo que la key de `MainWindow` evita gracias a su `TextBox` de ancho fijo.

### Tamaño de los botones: 18x18, igual que los overlay de `MainWindow`

El alto de fila (28) no admite el `GlyphButton` por defecto (26x26). Se reusa el mismo `Width="18" Height="18"` que ya usan `btnCopyKey`/`btnCopyParentKey` en `MainWindow.xaml`, en vez de definir un tercer tamaño.

### Habilitación de "abrir en el navegador" sin tocar el ViewModel

En vez de agregar `CanOpen` a `MiniTimerRowViewModel`, el botón de abrir en el code-behind del mini view calcula la misma condición in-place a partir de datos ya expuestos: `!string.IsNullOrEmpty(row.Summary)`. Es la misma regla que `issue-open-action` ya exige en general ("requiere un summary no vacío resuelto desde Jira"), sin necesitar un campo nuevo.

Alternativa descartada: exponer `CanOpen` en `MiniTimerRowViewModel` espejando `IssueViewModel`. Se descarta por pedido explícito de no tocar el ViewModel si no hace falta - el dato ya está disponible sin duplicar la propiedad.

### Extracción a un helper compartido

Se crea un helper estático WPF (junto a los demás helpers de `source/StopWatch/Helpers/`, que ya mezclan utilidades WinForms/WPF-aware como `ScreenPlacement.cs`) con dos responsabilidades independientes, ambas hoy atrapadas dentro de `MainWindow.xaml.cs`:

1. `FlashCopyConfirmation(Button button)` - swap del glyph a check y su restauración. Se mueve tal cual (no depende de `IssueViewModel`, solo de `Button`/`Path`/recursos `GlyphCheck`).
2. Construcción de la URL de "abrir en el navegador" a partir de `(string jiraBaseUrl, string issueKey)`, extraída de `OpenInBrowser` en `MainWindow.xaml.cs` (hoy acoplada a `IssueViewModel` solo para leer `IssueKey`/`CanOpen`). Cada ventana sigue decidiendo `CanOpen` con su propio dato antes de llamarla, y sigue siendo dueña de invocar `AppInfo.OpenUrl`.

`MainWindow.xaml.cs` pasa a llamar al helper en vez de sus métodos privados; no cambia su comportamiento observable.

Alternativa descartada: duplicar `FlashCopyConfirmation` en `MiniTimerWindow.xaml.cs`. Se descarta por pedido explícito de extraerlo a un helper compartido en cuanto se reutiliza en una segunda ventana.

### Sin nuevo requirement en `issue-open-action`

`issue-open-action` ya está redactado en términos de "una fila" en general, sin restringirlo a la ventana principal - la regla de habilitación se hereda tal cual, sin delta spec para esa capability.

## Risks / Trade-offs

- [Compresión del summary al hoverear la key puede sentirse "saltona" si el usuario mueve el mouse rápido entre filas] → Mitigación: es el mismo tipo de transición instantánea (sin animación) que ya usa `MainWindow` para sus propios botones de copiar; no se introduce timing nuevo que pueda fallar.
- [Extraer `FlashCopyConfirmation` y la construcción de URL cambia código que hoy es privado y probado indirectamente vía `MainWindow`] → Mitigación: el comportamiento observable de `MainWindow` no cambia (mismos íconos, mismo flujo); alcanza con verificar visualmente ambas ventanas tras la extracción.
- [Ancho mínimo de la vista mini (352px) queda más ajustado cuando la key es larga y ambos botones están visibles a la vez] → Mitigación: ya es el comportamiento aceptado en la ventana principal para el mismo ancho de key; el summary ya tiene `TextTrimming="CharacterEllipsis"` para absorber el espacio que falte.
