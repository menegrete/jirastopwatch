# Tasks

## 1. Spike y rama beta

- [x] 1.1 Confirmar que no existe otra rama `rc*` en `origin` y crear `rc-avalonia` desde `main`, que debe aparecer en `git branch -r`
- [x] 1.2 Crear una app Avalonia mínima (fuera de la solución, descartable) con una ventana sin borde, `Topmost` y `Position`, más un `TrayIcon`. Verificar a mano en Linux Mint 22 que la ventana se ubica donde se pide, queda encima y el ícono aparece en la bandeja
- [x] 1.3 En la misma spike, guardar y leer un valor con `ApplicationSettingsBase` en Linux y en Windows. Verificar que persiste y anotar la ruta de `user.config` (Decisión 5 del design)
- [x] 1.4 En la misma spike, guardar y leer un secreto con libsecret en Mint. Verificar con `secret-tool search` que quedó en el llavero

## 2. Shell Avalonia: app, bandeja y ventana principal

La UI se migra primero sobre `net10.0-windows`, para que la app se pueda ejecutar después de cada grupo. El paso a `net10.0` y la verificación en Linux ocurren en el grupo 7, cuando ya no quedan dependencias de Windows.

- [x] 2.1 Cambiar `StopWatch.csproj` a Avalonia (versión fijada en `Directory.Packages.props`), quitar `UseWPF`, `UseWindowsForms` y `MaterialDesignThemes`, mantener `net10.0-windows` por ahora y conservar `AssemblyName=StopWatch`. Reemplazar `System.Windows.Forms.Screen` por `Screens` de Avalonia. `dotnet build StopWatch.sln` debe pasar
- [x] 2.2 Portar `Theme.cs` / `ThemeBrushes.cs` a recursos por `ThemeVariant` sobre Fluent. Verificar que el cambio de tema en caliente aplica a todas las ventanas abiertas (spec `ui-theming`)
- [ ] 2.3 Portar `App.xaml(.cs)` y el `TrayIcon` con su menú. Verificar en Windows que `Tray` esconde la ventana y que un clic en el ícono la restaura (spec `minimize-behavior`)
- [ ] 2.4 Portar `MainWindow` y los estilos de `Styles.xaml` que usa (triggers → pseudo-clases). Verificar manualmente en Windows, en ambos temas, agregar, reordenar, quitar, iniciar y pausar issues, y la transición de color (specs `issue-list`, `ui-theming`)
- [ ] 2.5 Verificar en Windows que se cumple `main-window-placement`: posición y maximizado recordados, y reubicación si el monitor ya no existe

## 3. Ventanas secundarias

- [x] 3.1 Portar `SettingsWindow`. Fuera de Windows debe mostrar `Mini View` y `Tray`, sin `Taskbar Widget` ni selector de monitor. Agregar el test que trata `Taskbar Widget` como `Mini View` fuera de Windows (specs `minimize-behavior`, `taskbar-widget-view`)
- [x] 3.2 Portar `WorklogWindow` con el `TimePicker` de Avalonia y verificar que la carga de worklog con hora de inicio funciona igual que en 3.x (spec `worklog-start-time`)
- [x] 3.3 Portar `EditTimeWindow` y `ConfirmRemoveWindow`, y verificarlas manualmente en ambos temas

## 4. Vista mini y widget de taskbar

- [ ] 4.1 Portar `MiniTimerWindow`: arrastre, alineación a bordes, redimensionado horizontal, crecimiento lejos del borde y "siempre encima". Verificar cada escenario de `mini-timer-view` en Windows. El pegado a bordes no debe releer `Position` justo después de asignarla (Decisión 4 del design)
- [ ] 4.2 Portar `TaskbarWidgetWindow` a Avalonia manteniendo `TaskbarInterop` solo para Windows (lo usa por HWND). Verificar en Windows los escenarios de `taskbar-widget-view` (auto-hide, multi-monitor, pantalla completa)

## 5. Contrato de plugins 2.0

- [x] 5.1 Pasar `StopWatch.Plugin.Abstractions` a `net10.0` con tipos de Avalonia en `IPluginHost` y `ContractVersion = 2.0`. Actualizar `PluginLoaderTest` con los escenarios de `plugin-contract` (un 1.2 rechazado con ambas versiones visibles)
- [ ] 5.2 Reescribir `HelloWorld`, `HelloSqlite` y `SplitTime` en Avalonia con `contractVersion` 2.0. Verificar que el test de integración de samples los carga y que la ventana de `HelloWorld` se abre con el tema activo
- [x] 5.3 Commitear el cambio de contrato con footer `BREAKING CHANGE:`, que explica cómo recompilar un plugin, y verificar con `git log` que el mensaje lo incluye

## 6. Núcleo portable

- [x] 6.1 Crear `source/StopWatch.Core` (`net10.0`), agregarlo a `StopWatch.sln` y mover `Model/`, `Jira/`, `Settings/`, `Logging/`, los helpers puros y la lógica pura de `Update/`. `dotnet build StopWatch.sln` debe pasar sin advertencias CA1416
- [x] 6.2 Mover `ScreenPlacement` al núcleo sin depender de ninguna API de pantallas (recibe rectángulos) y sin depender de que exista una pantalla principal. Verificar que `ScreenPlacementTest` pasa
- [x] 6.3 Pasar `StopWatchTest` a `net10.0`, marcar con `[Platform("Win")]` los tests que usan Win32 y verificar que `dotnet test` pasa en Windows y en WSL

## 7. Servicios de plataforma y paso a Linux

- [x] 7.1 Definir `ISecretStore` en el núcleo. Implementar la variante Windows con `ProtectedData` y agregar un test que descifre un blob generado por el `DPApi.cs` actual
- [x] 7.2 Implementar `ISecretStore` para Linux con libsecret, asíncrono y sin fallback a texto plano. Agregar un test solo de Linux que guarde, lea y borre un secreto, y que falle de forma controlada sin llavero (spec `platform-support`)
- [x] 7.3 Implementar la selección de servicios por plataforma al arrancar y eliminar `DPApi.cs`. Verificar que en Windows la app sigue conectándose a Jira con el token ya guardado
- [x] 7.4 Pasar `StopWatch.csproj` a `net10.0`, con el código solo de Windows protegido por `OperatingSystem.IsWindows()`. `dotnet build` debe pasar sin advertencias CA1416 y `dotnet publish -r linux-x64` debe generar un ejecutable
- [ ] 7.5 Verificar en Linux Mint lo que los grupos 2 a 5 verificaron en Windows: bandeja, ventana principal y su posición, ventanas secundarias, vista mini, cambio de tema y ventana de `HelloWorld`

## 8. Auto-update y release multiplataforma

- [x] 8.1 Agregar la plataforma a `ReleaseAssetSelector` (nombres de Windows sin cambios, sufijo `linux-x64` en Linux) y cubrirlo en `AutoUpdateServiceTest` (spec `auto-update`)
- [ ] 8.2 Implementar `IUpdateInstaller` para Linux (extraer el `.tar.gz`, script `sh` que espera el PID, `mv`, `chmod +x`, relanzar) y verificar a mano una actualización entre dos builds locales en Mint, incluido el caso de directorio no escribible
- [x] 8.3 Extender `scripts/publish-release-artifacts.js` y `release.config.js` para publicar los dos `.tar.gz` de Linux y sus `.sha256`. Verificar con una ejecución local del script que los nombres coinciden con la spec `automated-releases` y que `tar -tvf` muestra `0755` en el ejecutable
- [ ] 8.4 Agregar `os: [windows-latest, ubuntu-latest]` a la matriz del job `build` en `.github/workflows/build.yml` y verificar en el PR que ambas corridas pasan y que `release` depende de las dos
- [x] 8.5 Actualizar `CLAUDE.md` (arquitectura, comandos de publish para Linux, fin de la convivencia WPF/WinForms) y verificar que los comandos documentados funcionan tal como están escritos

## 9. Beta y prueba humana en ambas plataformas

- [ ] 9.1 Instalar a mano el build de CI de la rama en Windows y en Mint y correr el recorrido básico (agregar issue, cronometrar, cargar worklog) antes del primer merge a `rc-avalonia`
- [ ] 9.2 Mergear a `rc-avalonia`. Verificar que GitHub publicó `4.0.0-rc.1` marcada como pre-release, con los ocho assets, y que `releases/latest` sigue apuntando a 3.x
- [ ] 9.3 Prueba humana en Windows 11: actualización automática desde 3.16 con suscripción beta, conservación de issues, tiempos, tema y token, bandeja, vista mini, widget de taskbar y plugins 1.x informados como incompatibles
- [ ] 9.4 Prueba humana en Linux Mint 22: instalación desde el `.tar.gz`, token en el llavero, bandeja, vista mini (bordes, posición recordada, siempre encima), worklog, cambio de tema y actualización automática entre dos rc
- [ ] 9.5 Registrar el resultado de 9.3 y 9.4 (versión probada, quién y cuándo, problemas encontrados) en el PR `rc-avalonia → main`. Corregir por PR a `rc-avalonia` y repetir hasta tener ambas plataformas aprobadas
- [ ] 9.6 Con la aprobación humana registrada, mergear `rc-avalonia → main` y verificar que se publica `4.0.0` estable con los ocho assets y que una instalación 3.x del canal estable la recibe
