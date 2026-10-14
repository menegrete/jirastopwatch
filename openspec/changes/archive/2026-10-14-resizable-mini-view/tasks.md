# Tasks

## 1. Límites del ancho

- [x] 1.1 Reutilizar `ScreenPlacement.ClampWidth(desired, minimum, workingArea)`, que ya existe (la usa la ventana principal) y ya tiene tests en `StopWatchTest/ScreenPlacementTest.cs` para dentro de rango, por debajo del mínimo, por encima del máximo y pantalla más angosta que el mínimo; verificar con `dotnet test StopWatch.sln --settings .runsettings --filter "FullyQualifiedName~ScreenPlacementTest"`

## 2. Persistencia

- [x] 2.1 Agregar el setting `MiniViewWidth` (Int32, por defecto 0) en `Properties/Settings.settings` y la propiedad correspondiente en `Settings/Settings.cs` (`ReadSettings` y `Save`), siguiendo `MiniViewLocation`; verificar que `dotnet build StopWatch.sln` compila sin warnings y que el test existente de `Settings`, si lo hay, sigue pasando

## 3. Redimensionado en la ventana

- [x] 3.1 En `MiniTimerWindow.xaml(.cs)`, reemplazar `Width="352"` por una constante de ancho por defecto y agregar la constante de ancho mínimo (340 px, igual al ancho por defecto); verificar que la vista mini sigue apareciendo con 352 px sin haber redimensionado nada
- [x] 3.2 Agregar dos `Thumb` transparentes (izquierdo y derecho, ~6 px, `Cursor="SizeWE"`) sobre el margen de sombra, y manejar `DragDelta`: nuevo ancho con `ClampWidth` (máximo = ancho del `WorkingArea` de la pantalla actual), el borde izquierdo mueve `Left` y `Width` juntos para que el derecho quede fijo, y pasar por `EnsureOnScreen`; verificar a mano que arrastrar cada borde ensancha y achica, que el borde opuesto no se mueve y que no se pasa del mínimo ni del ancho de la pantalla
- [x] 3.3 Al terminar el arrastre (`DragCompleted`) guardar `MiniViewWidth` y la posición; verificar que el ancho no se escribe durante el arrastre y que cerrar y reabrir la aplicación deja la vista mini con el mismo ancho y posición
- [x] 3.4 En `ShowAt`, aplicar el ancho guardado (o el por defecto si es 0) con `ClampWidth` antes de calcular la posición; verificar con un ancho guardado mayor que la pantalla (editando el setting) que aparece con el máximo que cabe

## 4. Comportamiento con el resto de la vista

- [x] 4.1 Verificar a mano con varias filas que todas adoptan el nuevo ancho, que el alto y la expansión hacia el borde de anclaje no cambian, que el summary muestra más texto al ensanchar, y que en el mínimo siguen visibles key, tiempo, pausa y el botón de restaurar (con key largo y tiempo de varias horas); ajustar la constante mínima si no
- [x] 4.2 Verificar que arrastrar desde el centro mueve la ventana sin cambiar el ancho, que arrastrar un borde no la mueve, y que el pausa/reanudar sigue sin mover la ventana; probar también con un monitor de otro DPI si hay uno disponible

## 5. Specs y cierre

- [x] 5.1 Revisar los escenarios de `specs/mini-timer-view/spec.md` contra el comportamiento real, correr `dotnet test StopWatch.sln --settings .runsettings` y `openspec validate resizable-mini-view`
- [ ] 5.2 Commit `feat:` y PR con `Closes #61` al inicio del cuerpo
