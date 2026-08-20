## 1. Fundamentos del tema

- [x] 1.1 Crear la clase de tema con los 12 tokens semánticos de `design.md` D4, y las dos instancias (oscura y clara) como constantes
- [x] 1.2 Verificar con una herramienta de contraste que cada par texto/fondo de la paleta oscura cumple 4.5:1 (texto principal) y 3:1 (texto atenuado); ajustar valores si alguno no llega
- [x] 1.3 Implementar el aplicador recursivo que recorre el árbol de controles y hace dispatch por tipo, dejando el default en `Background`/`Text`
- [x] 1.4 Exponer el tema activo de forma accesible desde los forms y desde `IssueControl` sin pasarlo por constructor en cada nivel

## 2. Fixes preexistentes (independientes del theming)

- [x] 2.1 Arreglar `IssueControl.Current`: agregar backing field, hacer que el getter lo devuelva y que el setter lo guarde antes de aplicar el color
- [x] 2.2 Fijar `startDatePicker.Format = Custom` y `CustomFormat = "dd/MM/yyyy"` en `WorklogForm.Designer.cs`
- [x] 2.3 Ajustar el ancho de `startDatePicker` para que los diez caracteres de la fecha entren completos, y verificar en la app corriendo que no queda recortado
- [x] 2.4 Confirmar que `startTimePicker` sigue en `"HH:mm"` y que ambos campos siguen siendo editables y se envían correctamente

## 3. Rutear los colores de runtime por el tema

- [x] 3.1 `IssueControl`: reemplazar `GradientInactiveCaption`/`Window` del setter de `Current` por `SurfaceActive`/`Surface`
- [x] 3.2 `IssueControl`: reemplazar `Color.PaleGreen`/`SystemColors.Control` del campo de timer por `TimerRunning`/`SurfaceDisabled`, invirtiendo el rol de fondo y texto
- [x] 3.3 `IssueControl`: reemplazar los `Color.Black` de las líneas separadoras del owner-draw del ComboBox por `Border`
- [x] 3.4 `MainForm`: reemplazar `Color.DarkGreen`/`Color.Tomato`/`ControlText` del estado de conexión por `Success`/`Danger`/`Text`
- [x] 3.5 `EditTimeForm`: reemplazar los 4 sitios de `Color.Tomato`/`SystemColors.Window` por `Danger`/`Surface`
- [x] 3.6 `WorklogForm`: reemplazar los 9 sitios de `Color.Tomato`/`SystemColors.Window` por `Danger`/`Surface`
- [x] 3.7 Buscar en todo `source/StopWatch/UI/` que no quede ningún literal de color ni referencia a `SystemColors` fuera de la definición de la paleta

## 4. Aplicar el tema por form

- [x] 4.1 Invocar el aplicador en el constructor de `MainForm` después de `InitializeComponent`
- [x] 4.2 Invocar el aplicador sobre cada `IssueControl` recién instanciado en `InitializeIssueControls()`, antes de agregarlo a `pMain`
- [x] 4.3 Verificar que las filas se themean también cuando `InitializeIssueControls()` se dispara por cambio de settings (los 4 call sites)
- [x] 4.4 Invocar el aplicador en `SettingsForm`, `WorklogForm`, `EditTimeForm` y `AboutForm`
- [x] 4.5 Configurar el `ToolTip` de `IssueControl` en `OwnerDraw` y pintarlo con los colores del tema

## 5. Controles que ignoran BackColor — parte mecánica

- [x] 5.1 Pasar los 4 separadores de `SettingsForm` de `BorderStyle.Fixed3D` a `None` con fondo `Border`
- [x] 5.2 Pasar los bordes de los 10 campos de texto de `Fixed3D` a un borde dibujado por la aplicación con color `Border`
- [x] 5.3 Pasar los 9 botones a `UseVisualStyleBackColor = false` con `FlatStyle.Flat`, y definir los colores de hover y pressed en `FlatAppearance`
- [x] 5.4 Dibujar el borde del `GroupBox` de estimación de `WorklogForm` con color `Border`, conservando el texto del título
- [x] 5.5 Ajustar los `LinkLabel` de `SettingsForm` y `AboutForm` al color `Link`, incluyendo los estados visitado y activo

## 6. Controles que ignoran BackColor — owner-draw

- [x] 6.1 Implementar el dibujado del glifo de CheckBox con los estados marcado, sin marcar, con el puntero encima y deshabilitado
- [x] 6.2 Aplicarlo a los 6 CheckBox de `SettingsForm` y verificar que el estado marcado se distingue claramente
- [x] 6.3 Implementar el dibujado del glifo de RadioButton con los mismos estados
- [x] 6.4 Aplicarlo a los 4 RadioButton de `WorklogForm`
- [x] 6.5 Implementar el dibujado del chrome de ComboBox: fondo, borde, flecha y área de texto
- [x] 6.6 Aplicarlo a los 4 ComboBox, verificando que el owner-draw ya existente de `cbJira` sigue funcionando y que el desplegable se ve oscuro

## 7. Elementos dibujados por el sistema

- [x] 7.1 Agregar `DwmSetWindowAttribute` a `NativeMethods.cs` y aplicar `DWMWA_USE_IMMERSIVE_DARK_MODE` a las 5 ventanas, con fallback al atributo 19 en builds anteriores
- [x] 7.2 Agregar `SetWindowTheme` a `NativeMethods.cs`, envuelto para que un fallo no propague excepción
- [x] 7.3 Aplicar `DarkMode_Explorer` al scrollbar de `pMain` y verificar con suficientes filas para que aparezca
- [x] 7.4 Aplicar `DarkMode_Explorer` al scrollbar del campo de comentario multilínea de `WorklogForm`

## 8. Iconos

- [x] 8.1 Agregar la variante clara del icono de engranaje a `icons/` y a los recursos
- [x] 8.2 Seleccionar la variante según el tema activo
- [x] 8.3 Revisar los 12 iconos sobre el fondo oscuro real y confirmar que ninguno queda indistinguible

## 9. Verificación

- [x] 9.1 Compilar y abrir las 5 ventanas; confirmar que ninguna presenta un rectángulo claro
- [x] 9.2 Recorrer los ciclos de estado: timer arranca y se detiene, validación falla y se corrige, issue se selecciona, conexión se establece y falla; confirmar que ningún control queda con colores claros
- [x] 9.3 Verificar hover, pressed y deshabilitado en botones, casillas, radios y combos
- [x] 9.4 Capturar las 5 ventanas en oscuro y compararlas con las capturas en claro para detectar regresiones de legibilidad
- [x] 9.5 Confirmar el comportamiento de degradación: que la app funcione en una versión de Windows donde `SetWindowTheme` no aplique

## 10. Selector de tema

- [x] 10.1 Agregar el campo de tema a `Properties.Settings` con default oscuro, y exponerlo en `Settings.cs` (`ReadSettings` y el guardado), siguiendo el patrón de los settings existentes
- [x] 10.2 Verificar que una instalación existente que se actualiza arranca en oscuro y conserva el resto de su configuración
- [x] 10.3 Agregar el selector de tema a `SettingsForm`
- [x] 10.4 Reaplicar el tema a `MainForm` y sus controles fijos al confirmar settings; verificar que las filas de issues ya se themean por la reconstrucción existente
- [x] 10.5 Verificar que cancelar el diálogo de settings no cambia el tema
- [x] 10.6 Verificar que la selección persiste entre ejecuciones

## 11. Verificación del tema claro

- [x] 11.1 Recorrer las 5 ventanas con el tema claro y confirmar que ningún elemento queda oscuro sobre fondo claro
- [x] 11.2 Verificar los owner-draw (glifos de casillas y radios, chrome de combos) con el tema claro
- [x] 11.3 Verificar contraste 4.5:1 / 3:1 también en la paleta clara
- [x] 11.4 Verificar que los iconos se leen en ambos temas, incluida la variante del engranaje
