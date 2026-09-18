## Why

Hoy minimizar la ventana principal tiene dos resultados posibles según el
checkbox "Minimize to tray": minimizarse a la taskbar (default) o esconderse
y dejar un ícono en la bandeja. En ningún caso queda algo del timer corriendo
a la vista — hay que restaurar la ventana para volver a verlo. La vista mini
ya resuelve exactamente eso, pero hoy solo se alcanza con un botón explícito
en la toolbar: minimizar, el gesto más natural para sacar la ventana del
medio sin cerrarla, nunca lleva ahí.

## What Changes

- El checkbox "Minimize to tray" se reemplaza por un combo con dos opciones:
  `Mini View` y `Tray`. El combo reemplaza por completo al checkbox: ya no
  existe un modo "minimizar sin hacer nada especial" a la taskbar.
- **BREAKING**: nuevas instalaciones, y quienes tenían "Minimize to tray"
  desactivado (el default anterior), arrancan con `Mini View` seleccionado.
  Minimizar la ventana principal deja de comportarse como el minimizado
  estándar de Windows para ese grupo, sin que lo hayan elegido explícitamente.
  Quienes lo tenían activado migran a `Tray`, sin cambio de comportamiento.
- Minimizar la ventana principal (el control nativo de Windows) pasa a
  disparar, según el combo:
  - `Mini View`: el mismo mecanismo que ya usa el botón "achicar a vista
    mini" de la toolbar (esconde la ventana principal, muestra la vista
    mini).
  - `Tray`: el comportamiento que hoy tiene "Minimize to tray" activado, sin
    cambios (esconde la ventana, muestra el ícono de bandeja).
- El botón existente en la toolbar para entrar a la vista mini a mano sigue
  funcionando igual, sin importar el valor del combo.

## Capabilities

### New Capabilities

- `minimize-behavior`: setting que decide adónde va la ventana principal al
  minimizarla (`Mini View` o `Tray`), reemplazando al checkbox
  `MinimizeToTray`, con su default y su migración desde el valor anterior.

### Modified Capabilities

- `mini-timer-view`: la vista mini pasa a alcanzarse también minimizando la
  ventana principal (cuando el setting está en `Mini View`), además del
  control explícito que ya existía en la toolbar.

## Impact

- `source/StopWatch/Settings/Settings.cs`,
  `source/StopWatch/Properties/Settings.settings`: el bool `MinimizeToTray`
  se reemplaza por un setting de tipo enumerado con los valores `MiniView` y
  `Tray`, con la migración del valor previo.
- `source/StopWatch/UI/SettingsWindow.xaml` (línea 67, `cbMinimizeToTray`) y
  `SettingsWindow.xaml.cs` (líneas 61, 70, 117): el checkbox se reemplaza por
  un combo de dos opciones.
- `source/StopWatch/UI/MainWindow.xaml.cs`:
  - `MainWindow_StateChanged` (líneas 320-343): en vez de ramificar solo por
    `MinimizeToTray`, al minimizar despacha a `EnterMiniView()` o al camino
    de tray existente según el setting nuevo.
  - `EnterMiniView()` (líneas 395-420) ya coerciona un `WindowState`
    recordado en `Minimized` a `Normal` al salir de la vista mini
    (`ExitMiniView()`, líneas 424-443), así que ese caso queda cubierto sin
    cambios adicionales.
  - `ShowOnTop()` (líneas 126-149): el camino que ya distingue `inMiniView`
    de "solo minimizado con tray" para una segunda instancia sigue
    aplicando sin cambios.
  - La infraestructura del tray icon (`ShowTrayIcon`/`HideTrayIcon`/
    `trayIcon_Click`/`DisposeTrayIcon`, líneas 313-386) no se toca: sigue
    siendo el camino para la opción `Tray` del combo.
