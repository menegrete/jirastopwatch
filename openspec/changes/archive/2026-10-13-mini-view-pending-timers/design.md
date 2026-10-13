# Design

## Context

La vista mini decide qué filas mostrar en `MiniTimerWindow.ResolveSources()`: si hay timers corriendo, lista `ActiveTimerViewModel.RunningSources`; si no, la única fila de `ActiveSource`. Ver proposal.md para la motivación.

Restricciones que condicionan el enfoque:

- Con "permitir varios" apagado, `PauseAllBut` solo **pausa** los demás timers, no los resetea. Un timer pausado con tiempo acumulado es normal en ese modo, así que "pausado con tiempo" no puede ser por sí solo el criterio para mostrar una fila.
- `MaxConcurrentTimers` limita solo los timers **corriendo**; no acota las filas pausadas.
- `ActiveTimerViewModel` es compartido con el widget de la barra de tareas, que solo lee las propiedades de una fila (`IssueKey`, `Elapsed`, `IsRunning`, etc.) y no usa `RunningSources`. Los ajustes fuerzan "permitir varios" a apagado cuando ese widget está seleccionado.
- La lógica de modelo debe poder probarse sin ventana (ver CLAUDE.md), y el modelo no conoce `Settings`.

## Goals / Non-Goals

**Goals:**

- Que la regla de "qué filas muestra la vista mini" viva en el modelo y sea probable con fakes.
- No alterar el comportamiento con "permitir varios" apagado, ni el del widget de la barra de tareas.

**Non-Goals:**

- Un tope propio de filas pausadas, scroll o `MaxHeight` en la lista.
- Cargar tiempo a Jira desde la vista mini (la spec mantiene que no abre diálogos).
- Cambiar `RunningSources` o el criterio de `ActiveSource`.

## Decisions

**1. Una propiedad nueva `TrackedSources` en `ActiveTimerViewModel`; `RunningSources` queda intacta.**
`TrackedSources` responde "qué filas debe mostrar una vista que sigue varios timers". Se calcula en `Refresh()` junto a `running`.
Alternativa descartada: ampliar `RunningSources` con los pausados. Cambiaría el significado de "quién corre" para cualquier otro consumidor, y el nombre dejaría de ser verdad.
Alternativa descartada: resolverlo en `MiniTimerWindow`. No se puede probar sin ventana y duplicaría la regla del modelo.

**2. La regla, con `allowMultiple` leído en cada `Refresh()`:**

```
allowMultiple = OFF  →  running.Count > 0 ? running : [ActiveSource]        (igual que hoy)
allowMultiple = ON   →  sources.Where(running || TimeElapsed > 0)
                        vacío → [ActiveSource]   (si hay)
```

Se conserva el orden de `sourcesProvider()`, que es el de la ventana principal; así pausar no mueve la fila.
El fallback a `ActiveSource` cuando ninguna fila califica mantiene el caso "ningún timer corrió todavía". Pasa a vivir en el modelo, no en `ResolveSources()`.

**3. `allowMultiple` se inyecta como `Func<bool>` en el constructor.**
Mantiene el modelo desacoplado de `Settings` y deja a los tests simular el setting con un lambda. `AppComposition` lo cablea con `() => settings.AllowMultipleTimers`. Se lee en cada `Refresh()` porque el usuario puede cambiar la opción con la app abierta.
Como un constructor con parámetro nuevo rompe a los llamadores existentes (`ActiveTimerViewModelTest`, `PluginLoaderTest`), se agrega una sobrecarga con un solo argumento que equivale a `allowMultiple = false`, para no tocar esos llamadores. Alternativa descartada: parámetro opcional, porque oculta la dependencia en el sitio de llamada.

**4. "Tiempo pendiente" es `WatchTimer.TimeElapsed > TimeSpan.Zero`.**
Es el mismo criterio que `IssueViewModel.CanReset`. `CanPost` redondea hacia arriba al minuto, así que en la práctica coincide; usar el tiempo crudo evita depender de ese redondeo. No se usa `ITimerSource` nuevo: ya expone `WatchTimer`, y así los fakes de los tests no cambian.

**5. Atenuado por un `DataTrigger` sobre `IsRunning` en la plantilla de fila.**
La plantilla ya enlaza `IsRunning` para la franja de color y para el ícono de pausa/play, así que no hace falta una propiedad nueva. El atenuado se aplica a toda fila pausada, incluida la fila única del modo apagado. La spec exige que con una sola fila el tamaño y la disposición no cambien, no el color, y una fila pausada ya se distinguía por su franja gris.

**6. `ResolveSources()` pasa a leer `TrackedSources`.**
`SyncRows` ya empareja filas por fuente y las agrega, quita o reordena, y se ejecuta cada segundo y tras cada clic. No requiere cambios: una fila pausada con tiempo simplemente deja de salir de la lista.

## Risks / Trade-offs

- [Las filas pausadas no están acotadas por `MaxConcurrentTimers`] → Se acepta: cada fila es tiempo que se debe a Jira y el tope natural es la lista de la ventana principal. Si resulta molesto, `MaxHeight` con scroll se agrega sin tocar la regla.
- [Al abrir la app con el estado guardado pueden reaparecer varias filas pausadas con tiempo, solo con "permitir varios" activado] → Es el comportamiento pedido; se mencionará en las notas del cambio.
- [Una fila pausada se puede reanudar pero el máximo de timers simultáneos puede impedirlo] → Ya lo cubre la capability `concurrent-timer-limit`: el intento no tiene efecto y la fila sigue pausada.
- [El modo apagado conserva el atenuado en la fila única pausada, que antes no lo tenía] → Cambio visual menor y deliberado; la franja gris ya la marcaba como pausada.
