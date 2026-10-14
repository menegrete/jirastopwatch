# Proposal

## Why

La vista mini tiene un ancho fijo, así que el summary de cada fila se trunca con "…" y el usuario no puede hacer nada para verlo más completo (o para achicar la vista cuando le sobra espacio). Issue #61.

## What Changes

- El usuario puede cambiar el ancho de la vista mini arrastrando su borde izquierdo o derecho, con el cursor de redimensionado sobre esos bordes.
- Solo se redimensiona el ancho. El alto sigue determinado por la cantidad de filas, y la lista sigue expandiéndose alejándose del borde de anclaje.
- El ancho tiene un mínimo que deja visibles el key, el tiempo, el control de pausa/reanudación y el botón de volver a la vista completa, y un máximo igual al ancho del área de trabajo de la pantalla donde está la vista.
- Al redimensionar desde el borde izquierdo, el borde derecho se queda quieto (y viceversa).
- El ancho elegido se conserva entre ejecuciones. Si ya no cabe en la pantalla donde se muestra, se ajusta a los límites.
- No incluye doble click para restablecer el ancho por defecto.

## Capabilities

### New Capabilities

### Modified Capabilities
- `mini-timer-view`: se agregan requisitos para redimensionar el ancho de la vista mini y para que ese ancho persista y siempre sea utilizable.

## Impact

- `source/StopWatch/UI/MiniTimerWindow.xaml(.cs)`: agarres de redimensionado en los bordes laterales y manejo del arrastre.
- `source/StopWatch/Helpers/ScreenPlacement.cs`: sin cambios; se reutiliza `ClampWidth`, que ya tiene tests.
- `source/StopWatch/Properties/Settings.settings` y `Settings/Settings.cs`: nuevo ajuste con el ancho de la vista mini.
- Sin cambios en `Model/` ni dependencias. Se publica como `feat:`.
