# Design

## Context

La motivación está en `proposal.md`. Este es el estado actual que condiciona el enfoque:

- `StopWatch` y `StopWatch.Plugin.Abstractions` apuntan a `net10.0-windows` con `UseWPF`. `StopWatch` además usa `UseWindowsForms`, solo para `NotifyIcon` y `Screen`. El design archivado `migrate-ui-to-wpf` declaró permanente esa convivencia porque WPF no tenía equivalentes.
- `Model/`, `Jira/`, `Settings/` y la lógica de `Update/` no referencian `System.Windows`. La UI (`UI/**`, unas 7.600 líneas) sí, y `Styles.xaml` concentra 76 `ControlTemplate`, 46 `Trigger` y 23 `DataTrigger`.
- Las dependencias nativas de Windows son `DPApi.cs` (crypt32), `NativeMethods.cs` y `TaskbarInterop.cs` (user32, dwmapi, shell32, gdi32), la lectura de `Registry` en `TaskbarWidgetWindow` y el script `.cmd` de `UpdateApplier`.
- El auto-update de las versiones 3.x elige el asset por nombre (`ReleaseAssetSelector`) y espera `StopWatch.exe` dentro del zip framework-dependent.
- `TreatWarningsAsErrors` está activo, así que CA1416 (API solo de Windows llamada desde código multiplataforma) rompe el build.

## Goals / Non-Goals

**Goals:**
- Un único código de UI en Avalonia para Windows y Linux.
- Que una instalación 3.x en Windows se actualice sola a 4.0 sin perder datos.
- Que ningún usuario del canal estable reciba la versión Avalonia antes de una prueba manual aprobada en ambas plataformas.

**Non-Goals:**
- macOS. Avalonia lo soportaría, pero no se compila, prueba ni publica.
- Wayland nativo. La app usa X11 o XWayland (ver Decisiones).
- Rediseñar la interfaz. Se migra el aspecto actual, no se cambia.
- Paquetes de distribución (`.deb`, Flatpak, Snap, AppImage). Se publica un `.tar.gz` y el usuario lo descomprime donde quiera.
- Linux ARM64.

## Decisions

### 1. Avalonia UI en lugar de Avalonia XPF o una UI web
Avalonia usa XAML y MVVM muy parecidos a WPF, y el modelo ya está separado de la UI. XPF ejecutaría el WPF existente sin reescribirlo, pero es una licencia comercial para un proyecto Apache 2.0 y no resuelve DPAPI, P/Invoke ni el updater. Una UI web descarta todo el XAML existente.

Se usa la última versión estable 12.x al empezar, fijada en `Directory.Packages.props`. Se usa el tema Fluent como base, con la paleta semántica de `Theme.cs` aplicada encima como recursos por `ThemeVariant`.

### 2. Un núcleo `net10.0` sin dependencias de UI
Nuevo proyecto `StopWatch.Core` (`net10.0`) con `Model/`, `Jira/`, `Settings/`, la lógica de `Update/` (selección de asset, semver, checksum, descarga) y `ScreenPlacement` (geometría pura). `StopWatch` (Avalonia) referencia al núcleo. La mayoría de los tests pasan a probar el núcleo y corren en ambas plataformas.

Alternativa descartada: dejar todo en un proyecto con `#if`. Es más difícil de leer y no permite que CA1416 marque las fugas de APIs de Windows al núcleo.

### 3. Servicios de plataforma detrás de interfaces
Lo que difiere por sistema operativo se define como interfaz en el núcleo e implementación en `StopWatch`, elegida al arrancar con `OperatingSystem.IsWindows()` / `IsLinux()`:

| Interfaz | Windows | Linux |
|---|---|---|
| `ISecretStore` (token) | DPAPI vía `System.Security.Cryptography.ProtectedData`, mismo formato que hoy | Secret Service vía P/Invoke a `libsecret-1.so.0` |
| `IUpdateInstaller` | Script `.cmd` actual | Script `sh` que espera el PID, reemplaza con `mv` (atómico en el mismo filesystem), aplica `chmod +x` y relanza |
| `ISystemThemeProbe` | Registry actual | `ActualThemeVariant` de Avalonia |
| Taskbar widget | `TaskbarInterop` actual | No existe |

`ProtectedData` reemplaza el código Obviex de `DPApi.cs` y lee el mismo blob, porque ambos usan `CryptProtectData` con alcance de usuario. Un test lo verifica con un blob generado por el código viejo.

Se elige libsecret porque Ubuntu y Mint traen GNOME Keyring por defecto y es el equivalente real de DPAPI. Si el llavero no responde, no hay fallback a texto plano (spec `platform-support`).

### 4. X11 en Linux, sin `UseWayland`
`AppBuilder.UsePlatformDetect()` elige X11 por defecto. En una sesión Wayland, el proceso corre sobre XWayland, que respeta `Position` y `Topmost`. No se llama `UseWayland()` ni `UseWaylandWithFallback()`. Así funcionan sin cambios la vista mini, `ScreenPlacement` y `main-window-placement`.

### 5. Settings: se conserva `ApplicationSettingsBase`
`Properties.Settings` (vía `System.Configuration.ConfigurationManager`) funciona en Linux y guarda `user.config` bajo `~/.local/share`. Mantenerlo evita migrar los settings en Windows. La ruta de `user.config` depende del nombre del ejecutable, la compañía y la ruta de instalación, así que se conservan `AssemblyName=StopWatch`, `AssemblyCompany` y el nombre `StopWatch.exe`. Antes de comprometerse se verifica en la spike (tarea 1.x). Si fallara en Linux, el plan B es un `settings.json` propio solo en Linux.

### 6. Contrato de plugins 2.0
`StopWatch.Plugin.Abstractions` pasa a `net10.0` y referencia `Avalonia` (solo el paquete base, no Desktop). `IPluginHost.MainWindow` pasa a ser `Avalonia.Controls.Window` y `Dispatcher` pasa a ser `Avalonia.Threading.Dispatcher`. `ContractVersion = 2.0`. `IsCompatible` ya exige el mismo major, así que los plugins 1.x quedan rechazados con el mensaje existente. Los samples se reescriben en Avalonia. El host y los plugins deben compartir la misma versión de Avalonia: el loader ya resuelve los assemblies compartidos desde el contexto del host.

### 7. Artifacts y selección de asset
Los nombres de Windows no cambian, para que el updater 3.x encuentre el 4.0. Linux agrega `JiraStopWatch-v{ver}-linux-x64-{self-contained|framework-dependent}.tar.gz`. Se usa `.tar.gz` y no `.zip` para conservar el bit de ejecución. `ReleaseAssetSelector` incorpora la plataforma: en Windows sigue usando los nombres actuales y en Linux usa el sufijo `linux-x64`. Ambos `dotnet publish -r linux-x64` corren desde el runner Windows del job `release`: Avalonia no necesita compilar código nativo para cross-publish. El empaquetado con permisos se hace con `tar` en ese runner, que ya trae `tar.exe` (bsdtar), y `--mode` fija `0755` en el ejecutable.

### 8. Tests en ambas plataformas
La matriz del job `build` suma la dimensión `os: [windows-latest, ubuntu-latest]` (sus pasos usan sintaxis de PowerShell, así que en Ubuntu se fija `shell: pwsh`). Los tests que tocan Win32 (por ejemplo, `TaskbarPlacementTest`) quedan marcados como solo de Windows con `[Platform("Win")]` de NUnit. El job `release` depende de ambos.

### 9. Lanzamiento por una rama beta con prueba humana
Ninguna parte de esta migración llega a `main` hasta que una persona la apruebe en ambas plataformas.

```
main ──●────────────────────────────────────────────●── 4.0.0 (estable)
        \                                          /
rc-avalonia ●──────●──────────●──────────────────●     ← un push publica 4.0.0-rc.N
             \    /  \       /                        (pre-release; solo beta)
feat/...      ●──●    fix/..●
                                    ▲
                        prueba manual en Windows 11,
                        Ubuntu 24.04 (Wayland) y Mint 22
                        con la checklist de tasks §9
```

- `rc-avalonia` sale de `main`. El nombre debe empezar con `rc` y no puede llevar `/`, porque ni el glob `rc*` del workflow ni el de `release.config.js` cruzan `/`.
- Las ramas de trabajo se mergean a `rc-avalonia` por PR. Cada push publica `4.0.0-rc.N` (el commit del contrato 2.0 lleva el footer `BREAKING CHANGE:`, así que la versión es mayor).
- Las pre-releases no tocan `main` ni `releases/latest`. Los usuarios estables no las ven.
- Mientras `rc-avalonia` vive, `main` se mergea en ella para traer hotfixes 3.x.
- El PR `rc-avalonia → main` se abre recién con la checklist aprobada por una persona en las tres plataformas, y su merge produce `4.0.0`.

## Risks / Trade-offs

- [Los suscriptores a betas reciben cada `4.0.0-rc.N` automáticamente en cuanto se publica] → Es el comportamiento buscado: ellos forman el grupo de prueba de la migración. Como precaución, antes de cada merge a `rc-avalonia` se prueba a mano el build de CI de la rama (tarea 9.1), para que una rc no deje roto lo básico.
- [Otro `rc*` activo comparte el contador `-rc.N`] → Antes de crear `rc-avalonia`, confirmar que no hay otra rama `rc*` (hoy no hay ninguna). No crear otra hasta cerrar esta.
- [La migración de `Styles.xaml` cambia sutilmente el aspecto o los estados] → Comparar capturas por ventana y tema contra 3.x en la checklist. La spec `ui-theming` (contraste y estados) es el criterio.
- [`user.config` queda en otra ruta en Windows y el usuario "pierde" su configuración] → Test manual de actualización 3.16 → 4.0.0-rc en la checklist. Ver Decisión 5.
- [GNOME sin la extensión AppIndicator no muestra bandeja] → Ubuntu la trae activada y Mint la soporta de forma nativa. Fuera de eso, `Tray` puede dejar la app sin ventana visible: si `TrayIcon` no se puede crear, se cae a `Mini View`.
- [XWayland desaparece en una versión futura de Ubuntu] → Hoy no está planeado. La vista mini dependería entonces de protocolos Wayland (layer-shell) y requeriría otro change.
- [Plugins de terceros dejan de cargar] → El mensaje de contrato incompatible ya existe. Se documenta la migración en las notas del release (footer `BREAKING CHANGE`).
- [La versión de Avalonia del host y la del plugin difieren] → Los plugins referencian Avalonia con `PrivateAssets` / `ExcludeAssets=runtime` y usan la del host. Se documenta en los samples.

## Migration Plan

1. Crear `rc-avalonia` desde `main` y desarrollar en ramas que se mergean a ella (Decisión 9).
2. Cada push a `rc-avalonia` publica una pre-release `4.0.0-rc.N` con los artifacts de Windows y Linux.
3. Una persona corre la checklist de tasks §9 sobre la última pre-release en Windows 11, Ubuntu 24.04 y Linux Mint 22, incluida la actualización automática desde 3.x.
4. Con la checklist aprobada, se abre el PR `rc-avalonia → main`. El merge publica `4.0.0` estable.

**Rollback**: antes del paso 4 no hace falta nada, porque `main` sigue intacto. Después del paso 4, se revierte el merge en `main` y se publica un `fix:`. Ese release sería `4.0.1`, con WPF de nuevo. El updater nunca baja de versión, así que quien ya tenga `4.0.0` recibe `4.0.1` como actualización normal.

## Open Questions

- Si las ventanas sin borde de Avalonia (vista mini) se ven bien sin compositor en algún escritorio liviano. No afecta a Ubuntu ni Mint. Se anota si aparece en la prueba manual.
