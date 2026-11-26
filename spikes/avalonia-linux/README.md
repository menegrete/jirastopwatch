# Spike de Avalonia en Linux

Aplicación desechable para verificar si Avalonia (usando X11/XWayland, sin `UseWayland`) respeta posición de ventana, Topmost, ventanas sin borde y bandeja del sistema. También prueba persistencia de configuración (`ApplicationSettingsBase`) y almacenamiento de secretos con libsecret.

Probar en: Ubuntu 24.04 (sesión Wayland) y Linux Mint 22 (Cinnamon, X11).

## Cómo ejecutarlo

1. Copia `avalonia-spike-linux-x64.tar.gz` a la máquina Linux.
2. Extrae y ejecuta:

   ```bash
   tar -xzf avalonia-spike-linux-x64.tar.gz
   chmod +x avalonia-spike   # solo si no quedó ejecutable
   ./avalonia-spike
   ```

3. Antes de empezar, anota el tipo de sesión: `echo $XDG_SESSION_TYPE` (`wayland` o `x11`). El spike también lo muestra en la ventana y en el log.

En Ubuntu (GNOME) la bandeja requiere la extensión AppIndicator; Ubuntu la trae habilitada.

## Checklist

Ventana mini (usa los botones de la ventana de control):

- [ ] "Open mini" abre la ventana mini (sin bordes, ~260x48, con el timer avanzando).
- [ ] Se mantiene encima de las ventanas de otras aplicaciones mientras el foco está en otra (Topmost).
- [ ] No aparece en la barra de tareas / dock.
- [ ] Los 4 botones de esquina la mueven a la esquina correcta del área de trabajo.
- [ ] "Move to X,Y" la mueve a la posición indicada.
- [ ] Arrastrar la ventana mini funciona.
- [ ] Tras soltar, la posición se mantiene (revisa el log: "requested → actual Position").
- [ ] Desmarcar/marcar "Topmost" tiene efecto.
- [ ] "Transparent + rounded": se ven las esquinas redondeadas y el fondo transparente (anota el `ActualTransparencyLevel` del log).

Bandeja:

- [ ] Aparece el icono en la bandeja.
- [ ] Clic en el icono muestra la ventana de control.
- [ ] El menú funciona: "Show control window", "Toggle mini", "Exit".
- [ ] Cerrar la ventana de control la oculta (la app sigue viva); "Exit" cierra la app.

Settings (sección "Settings"):

- [ ] El log muestra la ruta de `user.config`, si existe, `RunCount` y `LastValue`.
- [ ] Escribe un valor, pulsa "Save value", cierra la app con "Exit" y vuelve a abrirla: el valor y `RunCount` (que sube en 1 por arranque) se conservan.
- [ ] "Reload" relee el valor del archivo.
- [ ] Repite en Windows con `avalonia-spike.exe` si puedes (carpeta `win-x64`).

Secretos (sección "Secret storage", solo Linux):

- [ ] "Store secret" con un texto: el log dice OK y la longitud.
- [ ] En otra terminal: `secret-tool search account spike` muestra el ítem (etiqueta "Avalonia spike secret").
- [ ] "Lookup secret": el log indica longitud y si coincide con lo guardado (nunca imprime el valor).
- [ ] Cierra y reabre la app: "Lookup secret" sigue encontrándolo.
- [ ] "Clear secret" lo elimina (`secret-tool search account spike` ya no lo muestra).
- [ ] Con el llavero bloqueado o sin servicio corriendo (por ejemplo, sesión sin gnome-keyring), la app debe registrar un error en el log y no cerrarse. Anota el mensaje.

### Prueba rápida sin interfaz

`./avalonia-spike --selftest` guarda y lee un setting, y guarda, lee y borra un secreto en el llavero. No abre ventanas. Escribe el resultado (`PASS` o `FAIL`) en el log y termina con código 0 o 1. Si el llavero está bloqueado, el resultado puede tardar unos 40 segundos.

## Qué enviar de vuelta

- `~/avalonia-spike.log` (de cada máquina).
- Notas de qué punto del checklist falló o se comportó distinto.
- Capturas de pantalla (ventana mini, esquinas, bandeja).
- Resultado de `echo $XDG_SESSION_TYPE`.
