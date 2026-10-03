# Proposal

## Why

Con el change `add-beta-rc-releases` (issue #38) CI publica betas como
pre-releases `X.Y.Z-rc.N`, pero la app solo mira `releases/latest`, que
excluye pre-releases: quien quiere probar un beta tiene que bajarlo y
reemplazarlo a mano. Queremos que el usuario pueda optar por recibir betas a
través del auto-updater existente, sin afectar a quienes no lo activan.
Cierra el issue #39.

## What Changes

- Nuevo ajuste booleano "Subscribe to beta releases", **desactivado por
  defecto**, junto al ajuste existente de chequeo de actualizaciones, y
  persistido como el resto de los ajustes.
- **Apagado**: comportamiento idéntico al actual (solo releases estables,
  `releases/latest`).
- **Encendido**: el chequeo considera releases estables y pre-releases, y se
  ofrece la versión más alta según orden semver. Una `3.8.0` estable gana
  sobre cualquier `3.8.0-rc.N`, y `rc.10` gana sobre `rc.2`.
- Regla única, sin casos especiales: solo se ofrece una versión
  **estrictamente mayor** que la actual. Apagar el ajuste estando en un beta
  no baja ni ofrece volver a estable; se recibe la próxima estable mayor.
- El ajuste de chequeo general sigue siendo maestro: apagado, no hay
  actividad de red, con o sin beta.
- El aviso "update ready" indica cuando la actualización pendiente es un beta.
- La comparación de versiones pasa de `System.Version` a semver real, que
  entiende el sufijo de prerelease (hoy `Version.TryParse("3.8.0-rc.1")`
  falla) y tolera metadata `+build`.
- Los assets de un beta se encuentran con el sufijo completo en el nombre
  (`JiraStopWatch-v3.8.0-rc.1-...`), en vez de descartarlo.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `auto-update`: agrega el canal opcional de betas (ajuste, selección de la
  versión candidata con orden semver, no-downgrade, assets con sufijo, aviso
  de beta).

## Impact

- `source/StopWatch/Update/`: `UpdateVersion` (y una representación semver),
  `IReleaseSource`/`GitHubReleaseSource`, `AutoUpdateService`,
  `ReleaseInfo`, `PendingUpdate`.
- Settings: `Properties/Settings.settings`, `Settings/Settings.cs`,
  `UI/SettingsWindow.xaml(.cs)`; `UI/MainWindow.xaml.cs` (llamada al chequeo
  y texto del aviso).
- Tests NUnit/Moq: `UpdateVersionTest`, `AutoUpdateServiceTest`,
  `SettingsTest`, y los de selección de assets.
- Docs: README. Dependencia: #38 ya mergeado y verificado; los betas
  existen cuando se publique una rama `rc*`.
- Sin cambios en CI ni en el formato de los releases.
