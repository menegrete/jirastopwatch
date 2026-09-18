## 1. Helper compartido

- [x] 1.1 Crear `Helpers/GlyphButtonHelpers.cs` con `FlashCopyConfirmation(Button button)`, moviendo la lógica tal cual desde `MainWindow.xaml.cs` (swap del glyph a `GlyphCheck` y restauración del original).
- [x] 1.2 Agregar a ese helper (o a uno nuevo junto a él) la construcción de la URL de "abrir en el navegador" a partir de `(string jiraBaseUrl, string issueKey)`, extraída de `OpenInBrowser` en `MainWindow.xaml.cs`.
- [x] 1.3 Actualizar `MainWindow.xaml.cs` para llamar al helper en `btnCopyKey_Click`, `btnCopyParentKey_Click` y `OpenInBrowser`, sin cambiar su comportamiento observable.

## 2. Layout de la fila en la vista mini

- [x] 2.1 En `MiniTimerWindow.xaml`, envolver el `TextBlock` de la key (`TimerRow` DataTemplate) junto con dos nuevos `Button` (`btnCopyKey`, `btnOpen`, estilo `GlyphButton`, 18x18, `Visibility="Collapsed"`) en un contenedor `x:Name="keyArea"` dentro de la misma columna `Auto`.
- [x] 2.2 ~~Agregar un `Trigger SourceName="keyArea" Property="IsMouseOver" Value="True"` en `DataTemplate.Triggers` que ponga ambos botones en `Visible`.~~ Reemplazado: un `Grid` sin `Background` no es hit-testeable en sus huecos, así que el `Trigger` puro dejaba de detectar el hover justo al salir del texto de la key, antes de llegar al botón. En su lugar: `Background="Transparent"` en `keyArea` + handlers `MouseEnter`/`MouseLeave` en `MiniTimerWindow.xaml.cs` con un grace period de 300ms (`HoverGracePeriod`) antes de ocultar, para tolerar el hueco y saltos de mouse rápidos. Ver design.md, "El hover se resuelve en code-behind".
- [x] 2.3 Verificar visualmente que, al aparecer, los botones comprimen la columna `summary` (sin animación) y que el summary sigue truncándose con `CharacterEllipsis`; que al salir del hover, vuelven a `Collapsed` (tras el grace period) y el summary recupera su ancho. Confirmado por el usuario.

## 3. Comportamiento de los botones

- [x] 3.1 En `MiniTimerWindow.xaml.cs`, agregar el handler de `btnCopyKey` que copia `row.IssueKey` al portapapeles (`Clipboard.SetText`) y llama a `FlashCopyConfirmation`.
- [x] 3.2 Agregar el handler de `btnOpen` que arma la URL con el helper de la tarea 1.2 usando `settings.JiraBaseUrl` y `row.IssueKey`, y la abre con `AppInfo.OpenUrl` cuando `!string.IsNullOrEmpty(row.Summary)`; si esa condición no se cumple, el botón queda deshabilitado (bindeado a esa misma condición, sin acceder a `Source` para otra cosa).
- [x] 3.3 Confirmar que ninguno de estos dos handlers dispara `RowToggle_Click` (pausar/reanudar) ni interfiere con el drag de la ventana (`MiniTimerWindow_MouseLeftButtonDown`): son `Button` propios con su propio `Click`, igual que `btnRestore`/el botón de pausa ya existentes, que ya conviven con el drag del fondo sin problema.

## 4. Verificación

- [x] 4.1 Probar manualmente: hover sobre la key de una fila con summary resuelto muestra ambos íconos habilitados; copiar funciona y confirma con el check; abrir navega a la URL correcta. Confirmado por el usuario.
- [x] 4.2 Probar manualmente: fila cuyo summary todavía no resolvió muestra el ícono de abrir deshabilitado al hoverear la key. Confirmado por el usuario.
- [x] 4.3 Probar manualmente: hover sobre summary, tiempo o el control de pausa/reanudar de una fila no muestra los íconos de esa fila. Confirmado por el usuario.
- [x] 4.4 Correr `dotnet build StopWatch.sln` y `dotnet test StopWatch.sln --settings .runsettings` (TreatWarningsAsErrors está activo). Build: 0 warnings/0 errors. Tests: 155 passed, 5 skipped (preexistentes, requieren credenciales reales), 0 failed.
- [x] 4.5 Actualizar `CHANGELOG.md` bajo `[Unreleased]` (Added) con el resumen del cambio.
