# Inventario previo a la migración

Archivo de trabajo del change. Se levanta del código WinForms **antes** de
borrarlo, para tener contra qué contrastar el XAML y qué verificar al cerrar la
fase 2.

## 1.1 Atajos de teclado de `MainForm.ProcessCmdKey`

Todos operan sobre `issueControls[currentIssueIndex]`, salvo los dos primeros,
que mueven ese índice. Fuente: `UI/MainForm.cs:895-971` y los métodos `Issue*`
de `UI/MainForm.cs:975-1059`.

| # | Atajo | Método de `MainForm` | Acción exacta |
|---|-------|----------------------|---------------|
| 1 | `Ctrl+Up` | `IssueMoveUp` | Si el índice actual es 0, no hace nada. Si no, selecciona el issue anterior (`IssueSetCurrent(i-1)`). |
| 2 | `Ctrl+Down` | `IssueMoveDown` | Si el índice actual es el último, no hace nada. Si no, selecciona el siguiente (`IssueSetCurrent(i+1)`). |
| 3 | `Ctrl+P` | `IssueTogglePlay` | `IssueControl.StartStop()` — arranca o pausa el timer del issue actual. |
| 4 | `Ctrl+L` | `IssuePostWorklog` | `IssueControl.PostAndReset()` — abre el diálogo de worklog y postea. |
| 5 | `Ctrl+E` | `IssueEditTime` | `IssueControl.EditTime()` — abre el diálogo de edición de tiempo. |
| 6 | `Ctrl+R` | `IssueReset` | `IssueControl.Reset()` — pone el timer en cero. |
| 7 | `Ctrl+Delete` | `IssueDelete` | `IssueControl.Remove()` — quita la fila de la lista. |
| 8 | `Ctrl+I` | `IssueFocusKey` | `IssueControl.FocusKey()` — pone el foco en el combo del key del issue. |
| 9 | `Ctrl+N` | `IssueAdd` | Agrega una fila nueva a la lista (respetando `MaxIssues`). |
| 10 | `Ctrl+C` | `IssueCopyToClipboard` | `IssueControl.CopyKeyToClipboard()` — copia el key del issue actual. |
| 11 | `Ctrl+V` | `IssuePasteFromClipboard` | `IssueControl.PasteKeyFromClipboard()` — pega un key desde el portapapeles. |
| 12 | `Ctrl+O` | `IssueOpenInBrowser` | `IssueControl.OpenJira()` — abre el issue en el navegador. |
| 13 | `Alt+Down` | `IssueOpenCombo` | `IssueControl.OpenCombo()` — despliega el combo de keys del issue actual. |

Nota para el port: `IssueSetCurrent` además hace `pMain.ScrollControlIntoView`
y `Focus()` sobre la fila que pasa a ser la actual. En WPF eso se traduce a
`BringIntoView` sobre el contenedor de la fila seleccionada.

## 1.2 Controles de los formularios

### `MainForm.Designer.cs`

Tres paneles: `pTop`, `pMain` (la lista, con auto-scroll) y `pBottom`.

| Control | Tipo | Rol |
|---------|------|-----|
| `pTop` | Panel | Barra superior: contiene filtro, estado de conexión y accesos. |
| `lblActiveFilter` | Label | Rótulo del filtro. |
| `cbFilters` | ComboBox | Filtro de Jira activo (items `CBFilterItem` con Id/Name/Jql). Carga diferida en `DropDown`. |
| `lblConnectionStatus` | Label | Estado de conexión con Jira; clickeable, muestra el error. |
| `pbSettings` | PictureBox | Abre el diálogo de settings. |
| `pbMiniView` | PictureBox | Entra a la vista mini. |
| `pbHelp` | PictureBox | Abre la documentación en el navegador. |
| `pMain` | Panel | Contenedor de las filas de issue; hoy las posiciona a mano. |
| `pBottom` | Panel | Barra inferior. |
| `lblDivider` | Label | Línea divisoria (Label usado como regla horizontal). |
| `pbAddIssue` | PictureBox | Agrega una fila de issue. |
| `lblTotalTime` | Label | Rótulo del total. |
| `tbTotalTime` | TextBox | Suma de los tiempos de todas las filas (sólo lectura). |
| `notifyIcon` | NotifyIcon | Ícono de bandeja (ver 1.3). |
| `ttMain` | ToolTip | Tooltips de la barra, incluido el de `MaxIssues`. |

### `IssueControl` (designer embebido, `UI/IssueControl.cs:328-339`)

| Control | Tipo | Rol |
|---------|------|-----|
| `cbJira` | ComboBox | Key del issue, editable, con lista de issues del filtro. |
| `lblSummary` | Label | Summary del issue (hoy una línea fija, truncada). |
| `tbTime` | TextBox | Tiempo transcurrido; su color comunica corriendo/detenido. |
| `btnStartStop` | Button | Arranca y pausa el timer. |
| `btnReset` | Button | Pone el timer en cero. |
| `btnPostAndReset` | Button | Postea worklog y resetea. |
| `btnOpen` | Button | Abre el issue en el navegador. |
| `btnRemoveIssue` | Button | Quita la fila. |
| `ttIssue` | ToolTip | Tooltips de los botones de la fila. |

### `SettingsForm.Designer.cs`

| Control | Tipo | Rol |
|---------|------|-----|
| `lblJiraBaseUrl` / `tbJiraBaseUrl` | Label / TextBox | URL base de Jira. |
| `lblUsername` / `tbUsername` | Label / TextBox | Usuario. |
| `lblApiToken` / `tbApiToken` | Label / TextBox | API token. |
| `lblOpenAPITokensPage` | LinkLabel | Abre la página de tokens de Atlassian. |
| `splitter1` / `splitter2` / `splitter3` | Label | Separadores visuales. |
| `lblDisplayOptions` / `label1` | Label | Encabezados de sección. |
| `cbAlwaysOnTop` | CheckBox | Ventana siempre visible. |
| `cbMinimizeToTray` | CheckBox | Minimizar a la bandeja. |
| `cbAllowMultipleTimers` | CheckBox | Permitir varios timers corriendo. |
| `cbIncludeProjectName` | CheckBox | Incluir el nombre del proyecto en el summary. |
| `cbCheckForUpdate` | CheckBox | Chequear actualizaciones al arrancar. |
| `cbLoggingEnabbled` | CheckBox | Habilitar log. |
| `lblOpenLogFolder` | LinkLabel | Abre la carpeta del log. |
| `lblTheme` / `cbTheme` | Label / ComboBox | Tema (`Theme`). |
| `lblSaveTimerState` / `cbSaveTimerState` | Label / ComboBox | `SaveTimerSetting`. |
| `lblPauseOnSessionLock` / `cbPauseOnSessionLock` | Label / ComboBox | `PauseOnSessionLockSetting`. |
| `lblPostWorklogComment` / `cbPostWorklogComment` | Label / ComboBox | `WorklogCommentSetting`. |
| `lblStartTransitions` / `tbStartTransitions` | Label / TextBox | Transiciones a disparar al arrancar un timer. |
| `lblMaxIssues` / `nudMaxIssues` | Label / NumericUpDown | Cantidad máxima de filas. |
| `btnOk` / `btnCancel` / `btnAbout` | Button | Aceptar, cancelar, abrir About. |

**Agregar en la fase 2:** el setting de densidad de la lista (tarea 7.6).

### `WorklogForm.Designer.cs`

| Control | Tipo | Rol |
|---------|------|-----|
| `lblInfo` | Label | Resumen de qué se va a postear. |
| `lblComment` / `tbComment` | Label / TextBox | Comentario del worklog (multilínea). |
| `label1` | Label | Rótulo de la hora de inicio. |
| `startDatePicker` / `startTimePicker` | DateTimePicker | Fecha y hora de inicio del worklog. |
| `gbRemainingEstimate` | GroupBox | Agrupa las opciones de estimate restante. |
| `rdEstimateAdjustAuto` | RadioButton | Ajuste automático. |
| `rdEstimateAdjustLeave` | RadioButton | Dejar el estimate como está. |
| `rdEstimateAdjustSetTo` / `tbSetTo` | RadioButton / TextBox | Fijar el estimate a un valor. |
| `rdEstimateAdjustManualDecrease` / `tbReduceBy` | RadioButton / TextBox | Reducir el estimate en un valor. |
| `btnOk` / `btnSave` / `btnCancel` | Button | `DialogResult.OK` (postear), `DialogResult.Yes` (guardar sin postear), cancelar. |

### `EditTimeForm.Designer.cs`

| Control | Tipo | Rol |
|---------|------|-----|
| `lblHeader` | Label | Encabezado del diálogo. |
| `tbTime` | TextBox | Tiempo a fijar; señaliza error cuando no parsea. |
| `lblHint` | Label | Formato aceptado. |
| `btnOk` / `btnCancel` | Button | Aceptar, cancelar. |

### `AboutForm.Designer.cs`

| Control | Tipo | Rol |
|---------|------|-----|
| `pictureBox1` | PictureBox | Logo. |
| `lblNameVersion` | Label | Nombre y versión. |
| `lblHomepage` | LinkLabel | Sitio del proyecto. |
| `lblLicense` | LinkLabel | Licencia. |
| `textBox1` | TextBox | Texto de licencia / créditos (sólo lectura). |
| `btnClose` | Button | Cierra. |

## 1.3 Comportamiento observable de la bandeja

Fuente: `UI/MainForm.cs:291-320` (`MainForm_Resize`, `notifyIcon_Click`) y
`341-368` (`EnterMiniView`).

Estado de hoy:

1. **Plataforma.** Si no es Windows (`CrossPlatformHelpers.IsWindowsEnvironment()`
   es `false`), la bandeja no se toca nunca.
2. **Setting.** Si `MinimizeToTray` está apagado, la bandeja no se toca: la
   ventana minimiza como cualquier ventana y no aparece ícono.
3. **Aparece.** Con `MinimizeToTray` encendido, al pasar la ventana a
   `Minimized` el ícono se hace visible y la ventana se oculta (`Hide()`), así
   que desaparece también de la barra de tareas.
4. **Desaparece.** Al volver la ventana a `Normal`, el ícono se oculta.
5. **Restaurar.** Un click sobre el ícono hace `Show()` y pone la ventana en
   `Normal` — lo que a su vez dispara el punto 4.
6. **Interacción con la vista mini.** Mientras `inMiniView` es `true`,
   `MainForm_Resize` retorna temprano: la ventana principal está oculta a
   propósito y la vista mini ya es su representante, así que no debe haber
   además un ícono de bandeja. `EnterMiniView` fuerza
   `notifyIcon.Visible = false` al entrar.

Qué cambia en la fase 2 (ver `design.md` — riesgos): WPF no emite un
equivalente de `MainForm_Resize`, así que el disparador pasa a ser el estado de
la ventana (`Window.StateChanged` / `WindowState`), con las mismas cinco reglas
y la misma excepción por vista mini. El `NotifyIcon` sigue siendo el de
`System.Windows.Forms`.
