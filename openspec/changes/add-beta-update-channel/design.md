# Design

## Context

Ver `proposal.md` - Why. Estado actual de `source/StopWatch/Update/`:
`GitHubReleaseSource` pide `/releases/latest` (GitHub excluye drafts y
pre-releases) y `AutoUpdateService.CheckAndStageAsync` compara el tag contra
`AppInfo.Version` usando `UpdateVersion`, basado en `System.Version`, que no
parsea sufijos (`Version.TryParse("3.8.0-rc.1")` es falso). Luego arma los
nombres de asset con `latest.ToString()`, lo que descartaría el sufijo y no
encontraría `JiraStopWatch-v3.8.0-rc.1-...`. `AppInfo.Version` sale de
`AssemblyInformationalVersion`, que en un beta es `3.8.0-rc.1`. El servicio
se testea con Moq sobre `IReleaseSource`. La regla de producto ya decidida
(issue #39): solo se ofrece una versión estrictamente mayor que la actual.

## Goals / Non-Goals

**Goals:**
- Comparar versiones con semver real, con tests exhaustivos de la lógica.
- Cero cambios de comportamiento ni de red con el ajuste apagado.
- Mantener `IReleaseSource` como único seam hacia GitHub.

**Non-Goals:**
- Publicar los betas (CI, issue #38, ya hecho).
- Canales extra (alpha/nightly) o elegir un rc específico.
- Bajar de versión o volver a estable de forma activa.

## Decisions

**1. Tipo `SemanticVersion` propio y pequeño en `Update/`.**
Parsea `[v]MAJOR.MINOR.PATCH[-pre.release][+build]`, ignora la metadata de
build, implementa orden total según semver (sin sufijo > con sufijo para
igual `X.Y.Z`; identificadores del sufijo comparados uno a uno, numéricos
como números y menores que los alfanuméricos; menos identificadores es menor
si los previos empatan) y expone `IsPrerelease` y una forma canónica sin `v`
ni metadata (`3.8.0-rc.1`). `UpdateVersion.TryParse`/`IsNewer` pasan a
operar sobre él. Alternativa: el paquete NuGet `NuGet.Versioning`; descartada
por sumar una dependencia (versiones centralizadas en
`Directory.Packages.props`) para ~60 líneas de lógica bien acotada.
Alternativa: comparar strings a mano en el servicio; descartada, es justo
donde `rc.10` vs `rc.2` falla.

**2. La fuente devuelve candidatos; el servicio elige.**
`IReleaseSource.GetLatestReleaseAsync()` pasa a
`GetReleasesAsync(bool includePrereleases)`, que devuelve una lista de
`ReleaseInfo` (ahora con `IsPrerelease`). Apagado: sigue llamando a
`/releases/latest`, una sola llamada y comportamiento idéntico al actual
(devuelve una lista de un elemento). Encendido: llama a
`/releases?per_page=30`, descarta `draft` y devuelve todo el resto. Una
única llamada por chequeo (límite anónimo de 60/h por IP). Sin paginación:
30 releases sobran porque el orden de GitHub es por fecha de creación y el
candidato correcto siempre está entre los más recientes. La elección del
máximo semver vive en `AutoUpdateService` para testearla con fakes, sin
HTTP. Alternativa: que la fuente elija el máximo; descartada, mezcla
lógica de versionado con transporte.

**3. `AutoUpdateService.CheckAndStageAsync` recibe `includePrereleases`.**
Parsea el tag de cada candidato (descarta los que no parsean), con el flag
apagado ignora además cualquier `IsPrerelease` (defensa en profundidad),
toma el mayor y lo ofrece solo si es `IsNewer` que la versión actual
parseada. Esa única comparación implementa "estrictamente mayor, sin
downgrade", sin ramas especiales por canal. El `versionText` para nombres de
asset, carpeta de staging y `PendingUpdate.Version` es la forma canónica
con sufijo, no `Version.ToString()`. `PendingUpdate` gana `IsPrerelease`.
Si la versión actual no parsea, no se ofrece nada (igual que hoy).

**4. Ajuste `SubscribeToBetaReleases`, default `False`.**
Se agrega en `Properties/Settings.settings`, `Settings.cs`
(`ReadSettings`/`Save`) y un checkbox en `SettingsWindow` debajo de
"Automatically check for and install updates", con tooltip que avisa que los
betas pueden ser inestables, y habilitado solo si el chequeo general está
activo (binding de `IsEnabled` a `cbCheckForUpdates`). El chequeo general
sigue siendo maestro: `checkForUpdatesEnabled` falso corta antes de
cualquier llamada. `MainWindow.CheckForUpdatesAsync` pasa el valor del
ajuste; se aplica en el siguiente chequeo, sin forzar uno nuevo.

**5. Aviso de beta.** `MainWindow` arma el texto como `v{Version} (beta)
ready — restart to apply` cuando `PendingUpdate.IsPrerelease`; el link
"What's new" no cambia.

## Risks / Trade-offs

- [Un bug en la comparación semver ofrece una versión equivocada, incluso a
  usuarios estables] → Tests exhaustivos de parse y orden (incluye casos de
  la spec: `rc.10` vs `rc.2`, estable sobre rc, metadata de build), y el
  camino apagado no cambia: sigue `/releases/latest`.
- [Cambiar la firma de `IReleaseSource` rompe los mocks de los tests
  existentes] → Se actualizan en el mismo change; son pocos
  (`AutoUpdateServiceTest`).
- [La API pública de GitHub aplica rate limit a `/releases`] → Una llamada
  por chequeo, igual que hoy; los fallos ya se loguean y se reintentan en el
  próximo chequeo.
- [Con muchos más de 30 releases un candidato quedaría fuera] → Improbable
  por el orden por fecha; si algún día pasa, se pagina.
- [Un usuario en beta que apaga el ajuste queda en el beta hasta la próxima
  estable mayor] → Decisión de producto documentada (regla única).

## Migration Plan

Sin migración de datos: el ajuste nuevo nace en `False` y los usuarios
existentes conservan el comportamiento actual. Para validar de punta a punta
hace falta una pre-release real: publicar una `rc*` de prueba con el CI de
#38, correr un build con el ajuste activo y confirmar que descarga y
aplica el beta; luego borrar esa rama y sus releases de prueba. Rollback:
revertir el PR.
