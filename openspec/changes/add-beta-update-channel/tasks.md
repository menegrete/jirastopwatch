# Tasks

## 1. Versiones semver con prerelease

- [ ] 1.1 Crear `SemanticVersion` en `source/StopWatch/Update/` (parse de `[v]X.Y.Z[-pre][+build]`, metadata de build ignorada, orden semver, `IsPrerelease`, forma canónica sin `v` ni metadata) y adaptar `UpdateVersion.TryParse`/`IsNewer` a ese tipo. Verificar con `dotnet build StopWatch.sln` sin warnings.
- [ ] 1.2 Actualizar `UpdateVersionTest` con casos de parse (`v3.8.0-rc.1`, `3.8.0+abc123`, `3.8.0-rc.1+abc123`, entradas inválidas) y de orden (estable > su rc, `rc.10` > `rc.2`, `rc.1` > `beta.1` según identificadores, igual no es mayor, menor no es mayor). Verificar con `dotnet test StopWatch.sln --settings .runsettings --filter "FullyQualifiedName~UpdateVersionTest"`.

## 2. Fuente de releases y servicio de update

- [ ] 2.1 Agregar `IsPrerelease` a `ReleaseInfo` y cambiar `IReleaseSource.GetLatestReleaseAsync()` por `GetReleasesAsync(bool includePrereleases)`. En `GitHubReleaseSource`: apagado sigue llamando a `/releases/latest` (lista de un elemento); encendido llama a `/releases?per_page=30` y descarta drafts, leyendo `prerelease` de cada release. Verificar compilando y revisando que el camino apagado hace la misma llamada HTTP que antes.
- [ ] 2.2 Cambiar `AutoUpdateService.CheckAndStageAsync` para recibir `includePrereleases`: elegir el candidato de mayor semver entre los parseables (ignorando `IsPrerelease` si el flag está apagado), ofrecerlo solo si es estrictamente mayor que la versión actual, y usar la forma canónica con sufijo para nombres de asset, carpeta de staging y `PendingUpdate.Version`; agregar `PendingUpdate.IsPrerelease`. Verificar compilando.
- [ ] 2.3 Actualizar `AutoUpdateServiceTest` (mocks con la nueva firma) y agregar casos: apagado ignora pre-releases, encendido ofrece `rc.N` mayor, `rc.10` sobre `rc.2`, estable `3.8.0` sobre `3.8.0-rc.3`, actual `3.8.0-rc.1` sin ajuste y con estable `3.7.0` no ofrece nada (sin downgrade), llega `3.8.0` estable y se ofrece, asset y checksum con sufijo se encuentran y verifican, release beta sin assets no deja pendiente, chequeo general apagado no llama a la fuente con ajuste beta activo. Verificar con `dotnet test StopWatch.sln --settings .runsettings --filter "FullyQualifiedName~AutoUpdateServiceTest"`.

## 3. Ajuste y UI

- [ ] 3.1 Agregar `SubscribeToBetaReleases` (default `False`) en `Properties/Settings.settings` y en `Settings.cs` (`ReadSettings`/`Save`), con test en `SettingsTest` de que el default es falso y que persiste. Verificar con `dotnet test StopWatch.sln --settings .runsettings --filter "FullyQualifiedName~SettingsTest"`.
- [ ] 3.2 Agregar el checkbox "Subscribe to beta releases" en `SettingsWindow.xaml(.cs)` debajo del de actualizaciones, con tooltip de aviso de inestabilidad, habilitado solo si el chequeo general está activo, leído y guardado junto con los demás ajustes. Verificar abriendo la app: el checkbox persiste al reabrir Settings y se deshabilita al apagar el chequeo general.
- [ ] 3.3 En `MainWindow.CheckForUpdatesAsync`, pasar el ajuste al servicio y mostrar `v{Version} (beta) ready — restart to apply` cuando `PendingUpdate.IsPrerelease` (el texto estable no cambia). Verificar con `dotnet build` sin warnings y revisando el texto en ambos casos.

## 4. Documentación

- [ ] 4.1 Documentar en el README (sección "Beta builds") cómo suscribirse desde Settings, que es opt-in y que apagarlo no baja de versión; actualizar `CLAUDE.md` solo si algo descrito en la arquitectura del updater deja de ser cierto. Verificar releyendo las secciones y que `openspec validate add-beta-update-channel --strict` pasa.

## 5. Verificación de integración

- [ ] 5.1 Suite completa: `dotnet build StopWatch.sln` sin warnings y `dotnet test StopWatch.sln --settings .runsettings` en verde.
- [ ] 5.2 Prueba de punta a punta con una `rc*` de prueba (CI de #38): con un build local con ajuste activo y versión menor, confirmar que descarga, verifica y deja staged el beta con el aviso "(beta)"; con el ajuste apagado, que no ofrece nada. Luego borrar la rama de prueba y sus tags y pre-releases.
