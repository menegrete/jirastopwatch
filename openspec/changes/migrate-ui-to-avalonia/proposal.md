# Proposal

## Why

Jira StopWatch solo corre en Windows porque toda su interfaz está hecha en WPF y WinForms, que no existen en Linux. Hay usuarios que trabajan en Ubuntu y Linux Mint y hoy no pueden usarla. El modelo y la capa Jira ya no dependen de la UI, así que lo único que bloquea la portabilidad es la capa visual y un puñado de APIs nativas de Windows.

## What Changes

- Reemplazar WPF y WinForms por Avalonia UI en todas las ventanas (principal, vista mini, settings, worklog, edición de tiempo, confirmación) y en el ícono de bandeja, en Windows y en Linux. Ya no hay dos frameworks de UI en el mismo proceso.
- Separar el modelo, la capa Jira, los settings y la lógica de actualización en un núcleo que compila sin dependencias de Windows.
- Soportar Linux x64 (Ubuntu 24.04 LTS o posterior y Linux Mint 22 o posterior). La app corre sobre X11, o sobre XWayland en sesiones Wayland, para conservar el posicionamiento y el "siempre encima" de la vista mini.
- Guardar el token de Jira en el llavero del sistema (Secret Service) en Linux. En Windows se mantiene DPAPI, así que el token ya guardado sigue siendo legible.
- El widget de la barra de tareas queda disponible solo en Windows. En Linux, Configuración ofrece las opciones de minimizado `Mini View` y `Tray`.
- Publicar en cada release dos artifacts de Linux (self-contained y framework-dependent, `.tar.gz`) con sus checksums, sin cambiar los nombres de los artifacts de Windows.
- El auto-update descarga el artifact de la plataforma y la variante que corren, y aplica la actualización también en Linux.
- Correr los tests automatizados en Windows y en Linux.
- **BREAKING**: el contrato de plugins pasa a la versión 2.0. `IPluginHost` expone la ventana principal y el dispatcher de Avalonia en lugar de los de WPF. Los plugins compilados contra 1.x se informan como no cargados por versión incompatible. Se reescriben los samples `HelloWorld`, `HelloSqlite` y `SplitTime`.
- El primer release con esta migración se publica como beta (`4.0.0-rc.N`) desde una rama `rc-avalonia`. Llega a `main`, y por lo tanto a los usuarios del canal estable, solo después de que una persona la pruebe manual y funcionalmente en Windows y en Linux.

## Capabilities

### New Capabilities
- `platform-support`: plataformas en las que corre la app, el servidor gráfico que usa en Linux, cómo protege las credenciales y dónde persiste los datos de usuario en cada sistema operativo.

### Modified Capabilities
- `plugin-contract`: los servicios del host exponen tipos de Avalonia y el contrato pasa a 2.0, incompatible con 1.x.
- `automated-releases`: cada release, estable o beta, adjunta además los artifacts de Linux, y los tests corren en ambas plataformas.
- `auto-update`: la descarga elige el artifact según la plataforma además de la variante, y la actualización desde la versión WPF conserva settings, token e issues.
- `minimize-behavior`: el control de minimizado existe también en Linux, sin la opción `Taskbar Widget`.
- `taskbar-widget-view`: el widget queda limitado a Windows.
- `ui-theming`: en Linux la barra de título la dibuja el gestor de ventanas del escritorio y sigue el tema del sistema.

## Impact

- **Código**: `source/StopWatch/UI/**` (unas 7.600 líneas de XAML y code-behind, incluido `Styles.xaml`), `App.xaml(.cs)`, `Helpers/DPApi.cs`, `Helpers/TaskbarInterop.cs`, `Helpers/NativeMethods.cs`, `Update/UpdateApplier.cs`, `Settings/Settings.cs`, `StopWatch.Plugin.Abstractions`, los tres samples y los tests de UI y plataforma.
- **Proyectos**: `StopWatch` y `StopWatch.Plugin.Abstractions` dejan `net10.0-windows`, `UseWPF` y `UseWindowsForms`. Se agrega un proyecto de núcleo `net10.0`.
- **Dependencias**: se agrega Avalonia. Se elimina `MaterialDesignThemes`, porque Avalonia ya trae `TimePicker`.
- **CI/release**: `.github/workflows/build.yml`, `release.config.js` y `scripts/publish-release-artifacts.js`.
- **Decisiones previas**: revierte el non-goal "WinForms permanente" del design archivado `migrate-ui-to-wpf`, que se justificaba porque WPF no tenía bandeja ni enumeración de pantallas. Avalonia tiene ambas.
- **Plugins de terceros**: los plugins existentes deben recompilarse contra el contrato 2.0.
