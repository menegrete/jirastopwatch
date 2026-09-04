# Fase 1 — Extracción del modelo (la aplicación sigue siendo WinForms)

Al terminar el grupo 5 la aplicación compila, corre y se comporta igual que
hoy, con la lógica de negocio fuera de los controles y cubierta por tests. Ese
es el punto de rollback de la fase 2 (ver `design.md` — D1).

## 1. Inventario previo

- [x] 1.1 Listar los 13 atajos de `MainForm.ProcessCmdKey` (línea 895 en adelante) con la acción exacta que dispara cada uno, en un archivo de trabajo del change
- [x] 1.2 Listar cada control de `MainForm.Designer.cs`, `SettingsForm.Designer.cs`, `WorklogForm.Designer.cs`, `EditTimeForm.Designer.cs` y `AboutForm.Designer.cs` con su rol, para tener contra qué contrastar el XAML
- [x] 1.3 Anotar el comportamiento observable de la bandeja: cuándo aparece y desaparece el ícono hoy, y cómo interactúa con la vista mini (`MainForm.cs:291-320`, `341-368`)

## 2. Modelo de issue

- [x] 2.1 Crear el modelo observable de un issue en `Model/` con key, summary, comentario, método y valor de estimate, `WatchTimer` y si es el issue actual (ver `design.md` — D2)
- [x] 2.2 Tests del modelo: notificación de cambios, y que el tiempo mostrado siga al `WatchTimer`
- [x] 2.3 Reemplazar en `IssueControl` el estado guardado en controles por lecturas y escrituras contra el modelo, dejando los controles como presentación
- [x] 2.4 Mover la hidratación desde `Settings.PersistedIssues` (`MainForm_Shown`, líneas 245-271) y el volcado de vuelta (`SaveSettingsAndIssueStates`) para que operen sobre modelos
- [ ] 2.5 Verificar a mano: arrancar la aplicación con issues y timers persistidos y confirmar que se restauran igual que antes del cambio

## 3. Servicio de Jira

- [x] 3.1 Extraer `PostAndReset` (`IssueControl.cs:689`) a un servicio que reciba key, hora de inicio, tiempo, comentario y estimate por parámetro, sin tocar controles (ver `design.md` — D3)
- [x] 3.2 Tests del servicio sobre las reglas de `WorklogCommentSetting`: `WorklogOnly`, `CommentOnly` y el caso combinado, incluido que el comentario se limpie cuando corresponde
- [x] 3.3 Tests del servicio sobre el orden comentario → worklog → reset, y sobre que un fallo al postear el comentario impida el worklog y el reset
- [x] 3.4 Extraer la consulta de summary (`UpdateSummary`, línea 251) al servicio, con su tolerancia a `RequestDeniedException`
- [x] 3.5 Extraer la consulta de estimate restante (`UpdateRemainingEstimate`, línea 287) al servicio, devolviendo el resultado en lugar de escribir en un `WorklogForm`
- [x] 3.6 Tests de ambas consultas, incluido el caso de sesión inválida y el de key vacía

## 4. Filtro activo

- [x] 4.1 Introducir una dependencia que provea el JQL del filtro activo, y pasarla a quien la necesite (ver `design.md` — D3)
- [x] 4.2 Eliminar el `Application.OpenForms[0]` + `Controls.Find("cbFilters")` de `IssueControl.LoadIssues` (línea 734) y borrar el TODO que lo acompaña
- [x] 4.3 Mover la carga de filtros de `MainForm.LoadFilters` a esa misma dependencia
- [x] 4.4 Tests del proveedor de filtros, incluido el caso de que no haya filtro seleccionado

## 5. Concurrencia y cierre de fase

- [x] 5.1 Convertir el servicio de Jira a `async/await`, eliminando `Task.Factory.StartNew` de la lógica extraída (ver `design.md` — D4)
- [x] 5.2 Reemplazar los `InvokeIfRequired` que quedaron en los puntos de llamada por `await`, dejando `InvokeExtensions` sólo donde todavía haga falta bajo WinForms
- [x] 5.3 Correr `dotnet test` y confirmar que la suite previa sigue verde y los tests nuevos pasan
- [x] 5.4 Compilar en Release con `TreatWarningsAsErrors=true` y confirmar cero warnings
- [ ] 5.5 Verificar a mano el recorrido completo bajo WinForms: agregar issue, arrancar, pausar, editar tiempo, postear worklog con y sin comentario, cambiar filtro, cambiar tema, entrar y salir de la vista mini
- [ ] 5.6 Commit de cierre de fase 1 — punto de rollback

# Fase 2 — Migración a WPF (big bang)

## 6. Arranque y andamiaje

- [ ] 6.1 Agregar `App.xaml` y `App.xaml.cs`, y mover a ahí el mutex de instancia única, los manejadores de excepción no atrapada y la suscripción a `SessionSwitch` de `Program.cs`
- [ ] 6.2 Re-implementar la escucha de `WM_SHOWME` como hook sobre el handle de la ventana principal, en lugar de `WndProc` (ver `design.md` — D8)
- [ ] 6.3 Crear el diccionario de recursos con los estilos base, apoyado en `ThemeBrushes`, que ya expone el tema a WPF
- [ ] 6.4 Confirmar que `Theme` y `ThemeBrushes` cubren todos los colores que usaban los formularios; agregar los que falten

## 7. Lista de issues

- [ ] 7.1 Crear la colección observable de modelos de issue como fuente de la lista
- [ ] 7.2 Crear la plantilla de fila con key, summary, tiempo y los botones que tiene hoy `IssueControl`
- [ ] 7.3 Hacer que el summary use hasta dos líneas y trunque al final de la segunda, con el key y el tiempo siempre visibles — spec `issue-list`, "El summary se acomoda al ancho disponible"
- [ ] 7.4 Hacer que el alto de cada fila lo determine su contenido — spec `issue-list`, "Cada fila ocupa el alto que su contenido necesita"
- [ ] 7.5 Crear la segunda plantilla de fila para la densidad compacta y el mecanismo que elige entre las dos (ver `design.md` — D6)
- [ ] 7.6 Agregar el setting de densidad a `Settings`, exponerlo en el diálogo de settings y verificar que persiste — spec `issue-list`, "El usuario elige la densidad de la lista"
- [ ] 7.7 Portar agregar, quitar, reordenar y seleccionar issue sobre la colección, incluido el límite de `MaxIssues`

## 8. Ventana principal

- [ ] 8.1 Crear `MainWindow.xaml` con la barra superior, la lista y la barra inferior que tiene hoy `MainForm`
- [ ] 8.2 Hacer el ancho ajustable con un mínimo que mantenga key y tiempo visibles, persistirlo, y validar el ancho guardado contra la pantalla disponible reutilizando `ScreenPlacement` — spec `issue-list`, "El usuario ajusta el ancho de la ventana principal"
- [ ] 8.3 Derivar el alto de la suma de los altos de fila, sin permitir arrastrarlo, con el clamp al área de trabajo y el recorrido de la lista como desborde — spec `issue-list`, "El alto de la ventana se deriva de las filas" (ver `design.md` — D5)
- [ ] 8.4 Portar los 13 atajos de teclado del inventario 1.1 como `InputBindings`, verificando uno por uno contra `ProcessCmdKey` antes de borrarlo
- [ ] 8.5 Portar el ícono de bandeja usando `System.Windows.Forms.NotifyIcon`, con el disparador basado en el estado de la ventana en lugar de `MainForm_Resize`
- [ ] 8.6 Portar el estado de conexión con Jira, la autenticación y el chequeo de actualizaciones
- [ ] 8.7 Portar los tooltips, incluido el que cambia al llegar a `MaxIssues`
- [ ] 8.8 Adaptar la vista mini para que su ventana principal sea la nueva `MainWindow`, conservando el guardado y la restauración de posición y tamaño

## 9. Diálogos

- [ ] 9.1 `AboutWindow.xaml` — code-behind, misma información que `AboutForm`
- [ ] 9.2 `EditTimeWindow.xaml` — code-behind, con la validación de tiempo que tiene hoy y su señalización de error
- [ ] 9.3 `WorklogWindow.xaml` — code-behind, con las reglas de estimate y los tres resultados que hoy devuelve (`OK`, `Yes`, cancelar)
- [ ] 9.4 `SettingsWindow.xaml` — code-behind, con todos los settings actuales más el de densidad
- [ ] 9.5 Reemplazar `ModalDialog.ShowOver` por `Owner` + `Topmost` en los cuatro diálogos, y verificar que se ven por encima con "always on top" activado (ver `design.md` — D9)

## 10. Transiciones

- [ ] 10.1 Animar el color del campo de tiempo al arrancar y al pausar un timer, terminando en un color del tema activo — spec `ui-theming`, "Los cambios de estado se presentan con una transición"
- [ ] 10.2 Animar el cambio de fila seleccionada, con la animación nueva reemplazando a la anterior en vez de encolarse (ver `design.md` — D7)
- [ ] 10.3 Resolver el color de llegada contra el tema vigente al terminar la animación, para el caso de cambio de tema en curso
- [ ] 10.4 Verificar que el estado sigue siendo distinguible durante la animación y no sólo al final

## 11. Limpieza

- [ ] 11.1 Borrar `UI/MainForm.cs`, `UI/MainForm.Designer.cs`, `UI/IssueControl.cs`, `UI/SettingsForm.*`, `UI/WorklogForm.*`, `UI/EditTimeForm.*`, `UI/AboutForm.*`
- [ ] 11.2 Borrar `UI/ThemeApplier.cs` y `UI/ModalDialog.cs`
- [ ] 11.3 Borrar de `Helpers/NativeMethods.cs` las llamadas de modo oscuro (`UseImmersiveDarkMode`, `UseDarkScrollBars`, `UseDarkComboBox`, `AllowDarkModeForApp` y el `SetPreferredAppMode` por ordinal), conservando `WM_SHOWME` y lo que siga en uso
- [ ] 11.4 Borrar `Helpers/InvokeExtensions.cs` y `UI/ComboTextBoxEvents.cs` si ya no los usa nadie
- [ ] 11.5 Revisar `Program.cs`: dejarlo sólo si algo sigue necesitándolo, o borrarlo si `App.xaml.cs` lo reemplazó por completo
- [ ] 11.6 Revisar `UI/BoolConverters.cs` y `UI/ThemeIcons.cs`: adaptar o borrar según sigan aplicando
- [ ] 11.7 Confirmar que `UseWindowsForms` sigue en `true` en el `.csproj`, con un comentario que explique que es por la bandeja y la enumeración de pantallas

## 12. Verificación

- [ ] 12.1 Correr `dotnet test` y confirmar que la suite entera pasa sin haberla modificado desde la fase 1
- [ ] 12.2 Compilar en Release con `TreatWarningsAsErrors=true` y confirmar cero warnings
- [ ] 12.3 Recorrer los escenarios del spec `issue-list` uno por uno contra la aplicación corriendo
- [ ] 12.4 Recorrer los escenarios agregados del spec `ui-theming` uno por uno
- [ ] 12.5 Recorrer los escenarios del spec `ui-theming` que ya existían, para confirmar que la migración no los rompió
- [ ] 12.6 Recorrer los escenarios del spec `mini-timer-view`, que no debería cambiar
- [ ] 12.7 Verificar los 13 atajos del inventario 1.1 uno por uno
- [ ] 12.8 Verificar en una pantalla con escala distinta de 100% y en un arreglo de dos pantallas con escalas distintas
- [ ] 12.9 Verificar el comportamiento de la bandeja del inventario 1.3, incluida su interacción con la vista mini
- [ ] 12.10 Verificar la instancia única: abrir la aplicación dos veces y confirmar que la segunda trae la primera al frente
- [ ] 12.11 Verificar el bloqueo y desbloqueo de sesión con `PauseOnSessionLock` en cada uno de sus valores
