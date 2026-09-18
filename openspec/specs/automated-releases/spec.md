## Purpose

Automatiza la generación de un GitHub Release versionado y con changelog,
junto con los artifacts de build listos para ejecutar, cada vez que un cambio
release-able de la app llega a `main`, reemplazando el proceso manual de bump
de versión, changelog y publish.

## Requirements

### Requirement: El disparo del release se limita a cambios de código de la app

El proceso de release SHALL evaluarse únicamente para pushes a `main` que
incluyan al menos un archivo modificado bajo `source/StopWatch/**`.

#### Scenario: El push toca código de la app
- **WHEN** un push a `main` incluye un commit que modifica un archivo bajo
  `source/StopWatch/`
- **THEN** el proceso de release se evalúa para ese push

#### Scenario: El push solo toca archivos no relacionados
- **WHEN** un push a `main` solo modifica archivos fuera de
  `source/StopWatch/` (por ejemplo, `openspec/`, `CLAUDE.md`, o `.github/`)
- **THEN** el proceso de release no se evalúa y no se produce ningún release

### Requirement: El release solo se produce a partir de un historial de commits release-able

El proceso de release SHALL determinar la próxima versión a partir de los
mensajes de Conventional Commits (`feat`, `fix`, `BREAKING CHANGE`, etc.)
realizados desde el último tag de release, y SHALL NOT producir una nueva
versión, tag o release cuando ningún commit en ese rango lo amerite.

#### Scenario: Hay commits release-ables
- **WHEN** el historial de commits desde el último tag de release contiene al
  menos un commit `feat` o `fix` (o un commit de breaking change)
- **THEN** se calcula una nueva versión semántica (major para breaking
  changes, minor para `feat`, patch para `fix`) y se produce un release

#### Scenario: No hay commits release-ables
- **WHEN** se evalúa el proceso de release pero el historial de commits desde
  el último tag de release solo contiene commits no release-ables (por
  ejemplo, `chore`, `docs`, `refactor`, `test`)
- **THEN** no se produce ningún bump de versión, tag ni release

### Requirement: El release está condicionado a que los tests pasen

El proceso de release SHALL NOT producir un release para un commit cuya
corrida de tests automatizados no haya pasado.

#### Scenario: Los tests pasan
- **WHEN** la suite de tests del commit pusheado a `main` pasa
- **THEN** el proceso de release puede continuar evaluando si corresponde un
  release

#### Scenario: Los tests fallan
- **WHEN** la suite de tests del commit pusheado a `main` falla
- **THEN** no se produce ningún release para ese commit, sin importar su
  contenido

### Requirement: La versión publicada queda registrada en la app y en el changelog

Cuando se produce un release, el sistema SHALL actualizar los metadatos de
versión de la app y el changelog para reflejar la nueva versión, y SHALL
persistir esas actualizaciones de vuelta en `main`.

#### Scenario: Se actualizan los metadatos de versión
- **WHEN** se libera una nueva versión `X.Y.Z`
- **THEN** `AssemblyVersion`, `AssemblyFileVersion` y
  `AssemblyInformationalVersion` en los metadatos de assembly de la app
  quedan en `X.Y.Z`

#### Scenario: Se genera la entrada de changelog
- **WHEN** se libera una nueva versión `X.Y.Z`
- **THEN** se genera una entrada de changelog para `X.Y.Z` a partir de los
  mensajes de Conventional Commits incluidos en ese release, y se agrega al
  changelog del proyecto, sin necesidad de edición manual

### Requirement: El release incluye dos artifacts de build ejecutables

Cada release producido SHALL adjuntar un ejecutable self-contained y un
build framework-dependent zipeado al GitHub Release.

#### Scenario: Se adjunta el artifact self-contained
- **WHEN** se produce un release
- **THEN** se adjunta al release un ejecutable de Windows self-contained de
  un solo archivo (no requiere instalar el runtime de .NET por separado)

#### Scenario: Se adjunta el artifact framework-dependent
- **WHEN** se produce un release
- **THEN** se adjunta al release un archivo zip del build
  framework-dependent de un solo archivo (requiere tener instalado el .NET
  Desktop Runtime correspondiente en la máquina destino)

### Requirement: La app en ejecución muestra su propia versión

La ventana principal SHALL mostrar la versión actual de la app en su barra
de título nativa, junto al título de la app, leída desde los metadatos de
versión del assembly en ejecución.

#### Scenario: La barra de título muestra la versión
- **WHEN** se muestra la ventana principal
- **THEN** el texto de su barra de título incluye tanto el nombre de la app
  como la versión del assembly en ejecución (por ejemplo, "Jira StopWatch
  v2.4.0")

### Requirement: El release incluye checksums de integridad para sus artifacts

Cada release producido SHALL adjuntar un checksum SHA256 por cada uno de
los dos artifacts de build (self-contained y framework-dependent), como
archivos separados publicados junto a ellos en el mismo GitHub Release.

#### Scenario: Se publica el checksum del artifact self-contained
- **WHEN** se produce un release
- **THEN** se adjunta al release un archivo con el checksum SHA256 del
  ejecutable self-contained de esa versión

#### Scenario: Se publica el checksum del artifact framework-dependent
- **WHEN** se produce un release
- **THEN** se adjunta al release un archivo con el checksum SHA256 del zip
  framework-dependent de esa versión

### Requirement: El build self-contained es distinguible en runtime

El build self-contained SHALL poder identificarse a sí mismo como tal en
tiempo de ejecución, de forma que la app corriendo pueda determinar sin
ambigüedad si es la variante self-contained o la framework-dependent.

#### Scenario: La app corriendo es la variante self-contained
- **WHEN** la app fue publicada como build self-contained
- **THEN** la app, en ejecución, puede determinar que es la variante
  self-contained

#### Scenario: La app corriendo es la variante framework-dependent
- **WHEN** la app fue publicada como build framework-dependent
- **THEN** la app, en ejecución, puede determinar que no es la variante
  self-contained
