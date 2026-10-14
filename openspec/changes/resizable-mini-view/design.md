# Design

## Context

`MiniTimerWindow` es una ventana WPF sin marco, transparente (`WindowStyle="None"`, `AllowsTransparency="True"`, `ResizeMode="NoResize"`), con `Width="352"` fijo y `SizeToContent="Height"`. El pill visible está rodeado por un margen de 6 px (`ShadowMargin`) para la sombra, y toda la geometría de pantalla se maneja en píxeles de dispositivo mediante `PillSize`/`PillLocation`/`MovePillTo`/`GetScale`. La ventana se mueve con `MouseLeftButtonDown` y la posición se recuerda en `MiniViewLocation`. Ver proposal.md - Why.

## Goals / Non-Goals

**Goals:**
- Redimensionar solo el ancho, arrastrando los bordes laterales.
- Que el borde opuesto al arrastrado no se mueva.
- Persistir el ancho y tolerar cambios de pantalla.
- Lógica de límites pura y testeable.

**Non-Goals:**
- Redimensionar el alto o cambiar la escala/fuente.
- Restablecer el ancho con doble click.
- Que el ancho se adapte solo al largo del summary.

## Decisions

**Dos `Thumb` laterales en el margen de sombra, no `WindowChrome`.** Con transparencia, `ResizeBorderThickness` es poco predecible y el redimensionado nativo no deja controlar el borde fijo ni los límites. Los `Thumb` (izquierdo y derecho, ~6 px, `Cursor="SizeWE"`, fondo transparente para que haya hit-test) caen sobre la zona de sombra, que ya está vacía, y no compiten con el contenido. Se mantiene `ResizeMode="NoResize"`. Alternativa descartada: grip abajo a la derecha; no permite agarrar el borde izquierdo y el alto no es redimensionable.

**El borde izquierdo mueve `Left` y `Width` juntos, en píxeles de dispositivo.** En `DragDelta` se calcula el nuevo ancho del pill y, para el borde izquierdo, la nueva posición con `PillLocation`/`MovePillTo`, de modo que el borde derecho quede fijo. Reutiliza la conversión de DPI existente.

**Límites con `ScreenPlacement.ClampWidth` (ya existente).** Función pura, en píxeles, sin DPI, que ya usa la ventana principal: `(desired, minimum, workingArea)` devuelve el ancho ajustado, con el mínimo ganando si la pantalla es más angosta; se reutiliza tal cual y ya tiene tests. El mínimo es una constante de la ventana (suficiente para key + tiempo + pausa + el botón de restaurar de ~32 px; 340 px, igual al ancho por defecto, de modo que solo se puede ensanchar); el máximo sale del `WorkingArea` de la pantalla actual. Alternativa descartada: clamp inline en la ventana, porque queda sin test y el repo ya concentra esta matemática en `ScreenPlacement`.

**Durante el arrastre también se corrige la posición.** Tras cambiar el ancho se pasa por `EnsureOnScreen`, para que ensanchar contra un borde no deje el pill fuera del área de trabajo. Se hace también al aplicar el ancho guardado en `ShowAt`.

**Persistencia en `MiniViewWidth` (Int32, 0 = por defecto).** Nuevo ajuste en `Settings.settings`, leído y guardado en `Settings.cs` igual que `MiniViewLocation`. Se escribe al terminar el arrastre (`DragCompleted`), no en cada `DragDelta`; al terminar también se guarda la posición, porque el borde izquierdo la cambia. El valor 0 evita guardar el 352 y permite cambiar el valor por defecto sin migración. Al abrir, `ShowAt` aplica el ancho (o el por defecto) con `ClampWidth` antes de calcular la posición, ya que el pill debe tener su tamaño final para `EnsureOnScreen`.

**El ancho por defecto pasa a una constante en code-behind.** El `Width="352"` del XAML se reemplaza por la constante, para que el valor por defecto y el 0 compartan una sola fuente. El alto sigue en `SizeToContent="Height"`; `SyncRows` y el anclaje vertical no cambian.

## Risks / Trade-offs

[Zona de agarre angosta (6 px) y transparente, difícil de ver] → El cursor ↔ al pasar es la única pista; es consistente con una ventana sin marco. Si resulta difícil de agarrar, ampliar el `Thumb` hacia adentro del pill.
[Conflicto entre el arrastre de la ventana (`MouseLeftButtonDown`) y los `Thumb`] → Los `Thumb` capturan el mouse y marcan el evento como manejado; verificar manualmente que arrastrar un borde no mueve la ventana.
[Cambio de DPI entre pantallas durante el arrastre] → Todo se calcula en píxeles de dispositivo con la escala de la pantalla actual, igual que el resto de la posición.
[Mínimo mal calibrado] → Verificar con key largo, tiempo de varias horas y el texto de pausa; ajustar la constante.
