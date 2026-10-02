## MODIFIED Requirements

### Requirement: El disparo del release se limita a cambios de código de la app

El proceso de release SHALL evaluarse únicamente para pushes a `main` o a una
rama `rc*` que incluyan al menos un archivo modificado bajo
`source/StopWatch/**`.

#### Scenario: El push toca código de la app
- **WHEN** un push a `main` o a una rama `rc*` incluye un commit que modifica
  un archivo bajo `source/StopWatch/`
- **THEN** el proceso de release se evalúa para ese push

#### Scenario: El push solo toca archivos no relacionados
- **WHEN** un push a `main` o a una rama `rc*` solo modifica archivos fuera de
  `source/StopWatch/` (por ejemplo, `openspec/`, `CLAUDE.md`, o `.github/`)
- **THEN** el proceso de release no se evalúa y no se produce ningún release

### Requirement: El release está condicionado a que los tests pasen

El proceso de release SHALL NOT producir un release, estable o beta, para un
commit cuya corrida de tests automatizados no haya pasado.

#### Scenario: Los tests pasan
- **WHEN** la suite de tests del commit pusheado a `main` o a una rama `rc*`
  pasa
- **THEN** el proceso de release puede continuar evaluando si corresponde un
  release

#### Scenario: Los tests fallan
- **WHEN** la suite de tests del commit pusheado a `main` o a una rama `rc*`
  falla
- **THEN** no se produce ningún release para ese commit, sin importar su
  contenido

### Requirement: La versión publicada queda registrada en la app y en el changelog

Cuando se produce un release estable, el sistema SHALL actualizar los
metadatos de versión de la app y el changelog para reflejar la nueva versión,
y SHALL persistir esas actualizaciones de vuelta en `main`. Cuando se produce
una pre-release, SHALL registrar la versión en los metadatos de la app
compilada, pero SHALL NOT persistir nada de vuelta en la rama `rc*`.

#### Scenario: Se actualizan los metadatos de versión
- **WHEN** se libera una nueva versión estable `X.Y.Z`
- **THEN** `AssemblyVersion`, `AssemblyFileVersion` y
  `AssemblyInformationalVersion` en los metadatos de assembly de la app
  quedan en `X.Y.Z`

#### Scenario: Se genera la entrada de changelog
- **WHEN** se libera una nueva versión estable `X.Y.Z`
- **THEN** se genera una entrada de changelog para `X.Y.Z` a partir de los
  mensajes de Conventional Commits incluidos en ese release, y se agrega al
  changelog del proyecto, sin necesidad de edición manual

#### Scenario: Los metadatos de una pre-release llevan el sufijo solo donde es válido
- **WHEN** se libera una pre-release `X.Y.Z-rc.N`
- **THEN** `AssemblyVersion` y `AssemblyFileVersion` del build quedan en la
  parte numérica `X.Y.Z` y `AssemblyInformationalVersion` queda en
  `X.Y.Z-rc.N`, y el build compila correctamente

#### Scenario: Una pre-release no escribe de vuelta en su rama
- **WHEN** se produce una pre-release desde una rama `rc*`
- **THEN** no se crea ningún commit en esa rama y `CHANGELOG.md` no se
  modifica en el repositorio

## ADDED Requirements

### Requirement: Las ramas rc* publican pre-releases versionadas con sufijo rc

Un push a una rama `rc*` con commits release-ables, cuyos tests pasan, SHALL
producir un GitHub Release marcado como pre-release con versión
`X.Y.Z-rc.N`, donde `X.Y.Z` se calcula por Conventional Commits y `N` es un
contador que se incrementa con cada pre-release de esa versión base.

#### Scenario: Primera pre-release de una versión
- **WHEN** se pushea a una rama `rc*` un historial con un commit `feat` desde
  la última versión estable `3.7.0`
- **THEN** se publica una pre-release `3.8.0-rc.1`, marcada como pre-release
  en GitHub

#### Scenario: Pre-release subsiguiente
- **WHEN** ya existe `3.8.0-rc.1` y se pushea a la misma rama un nuevo commit
  release-able
- **THEN** se publica `3.8.0-rc.2`

#### Scenario: No hay commits release-ables en la rama rc
- **WHEN** un push a una rama `rc*` solo contiene commits no release-ables
- **THEN** no se produce ninguna pre-release

### Requirement: Las pre-releases no afectan al canal estable

La publicación de una pre-release SHALL NOT alterar lo que GitHub considera
el último release estable del repositorio, ni modificar `main`.

#### Scenario: El último release estable no cambia
- **WHEN** se publica una pre-release `3.8.0-rc.1`
- **THEN** el endpoint `releases/latest` del repositorio sigue devolviendo el
  último release estable previo

#### Scenario: El merge a main toma el número base
- **WHEN** una rama `rc*` con pre-releases `3.8.0-rc.N` se mergea a `main` y
  corresponde un release estable
- **THEN** el release estable es `3.8.0` y el merge no produce conflictos de
  changelog ni de versión

### Requirement: Las pre-releases incluyen los mismos artifacts que un release estable

Cada pre-release SHALL adjuntar el ejecutable self-contained, el zip
framework-dependent y el checksum SHA256 de cada uno, con la versión
completa, incluyendo el sufijo `-rc.N`, en el nombre del archivo.

#### Scenario: Assets de una pre-release
- **WHEN** se publica la pre-release `3.8.0-rc.1`
- **THEN** el release adjunta
  `JiraStopWatch-v3.8.0-rc.1-self-contained.exe`,
  `JiraStopWatch-v3.8.0-rc.1-framework-dependent.zip` y los dos archivos
  `.sha256` correspondientes
