# Spec Delta

## MODIFIED Requirements

### Requirement: El release incluye dos artifacts de build ejecutables

Cada release producido SHALL adjuntar un ejecutable self-contained y un
build framework-dependent zipeado al GitHub Release para Windows, y SHALL
adjuntar además un build self-contained y un build framework-dependent
empaquetados como `.tar.gz` para Linux x64. Los nombres de los artifacts
de Windows SHALL NOT cambiar respecto de los releases anteriores.

#### Scenario: Se adjunta el artifact self-contained
- **WHEN** se produce un release
- **THEN** se adjunta al release un ejecutable de Windows self-contained de
  un solo archivo (no requiere instalar el runtime de .NET por separado)

#### Scenario: Se adjunta el artifact framework-dependent
- **WHEN** se produce un release
- **THEN** se adjunta al release un archivo zip del build
  framework-dependent de un solo archivo (requiere tener instalado el .NET
  Desktop Runtime correspondiente en la máquina destino)

#### Scenario: Se adjuntan los artifacts de Linux
- **WHEN** se produce el release `4.0.0`
- **THEN** se adjuntan
  `JiraStopWatch-v4.0.0-linux-x64-self-contained.tar.gz` (no requiere
  instalar .NET) y `JiraStopWatch-v4.0.0-linux-x64-framework-dependent.tar.gz`
  (requiere el .NET Runtime correspondiente), y el ejecutable de cada uno
  conserva el permiso de ejecución al extraerlo

#### Scenario: Los nombres de Windows no cambian
- **WHEN** se produce el release `4.0.0`
- **THEN** los artifacts de Windows se llaman
  `JiraStopWatch-v4.0.0-self-contained.exe` y
  `JiraStopWatch-v4.0.0-framework-dependent.zip`, igual que en los
  releases 3.x

### Requirement: El release incluye checksums de integridad para sus artifacts

Cada release producido SHALL adjuntar un checksum SHA256 por cada uno de
sus artifacts de build (los dos de Windows y los dos de Linux), como
archivos separados publicados junto a ellos en el mismo GitHub Release.

#### Scenario: Se publica el checksum del artifact self-contained
- **WHEN** se produce un release
- **THEN** se adjunta al release un archivo con el checksum SHA256 del
  ejecutable self-contained de esa versión

#### Scenario: Se publica el checksum del artifact framework-dependent
- **WHEN** se produce un release
- **THEN** se adjunta al release un archivo con el checksum SHA256 del zip
  framework-dependent de esa versión

#### Scenario: Se publican los checksums de los artifacts de Linux
- **WHEN** se produce un release
- **THEN** se adjunta al release un archivo con el checksum SHA256 de cada
  uno de los dos `.tar.gz` de Linux de esa versión

### Requirement: Las pre-releases incluyen los mismos artifacts que un release estable

Cada pre-release SHALL adjuntar los mismos artifacts de Windows y de Linux
que un release estable, con el checksum SHA256 de cada uno, y con la
versión completa, incluyendo el sufijo `-rc.N`, en el nombre del archivo.

#### Scenario: Assets de una pre-release
- **WHEN** se publica la pre-release `4.0.0-rc.1`
- **THEN** el release adjunta
  `JiraStopWatch-v4.0.0-rc.1-self-contained.exe`,
  `JiraStopWatch-v4.0.0-rc.1-framework-dependent.zip`,
  `JiraStopWatch-v4.0.0-rc.1-linux-x64-self-contained.tar.gz`,
  `JiraStopWatch-v4.0.0-rc.1-linux-x64-framework-dependent.tar.gz` y los
  cuatro archivos `.sha256` correspondientes

### Requirement: El disparo del release se limita a cambios de código de la app

El proceso de release SHALL evaluarse únicamente para pushes a `main` o a una
rama `rc*` que incluyan al menos un archivo modificado bajo
`source/StopWatch/**`, `source/StopWatch.Core/**` o
`source/StopWatch.Plugin.Abstractions/**`.

#### Scenario: El push toca código de la app
- **WHEN** un push a `main` o a una rama `rc*` incluye un commit que modifica
  un archivo bajo `source/StopWatch/`
- **THEN** el proceso de release se evalúa para ese push

#### Scenario: El push solo toca el núcleo o el contrato de plugins
- **WHEN** un push a `main` o a una rama `rc*` solo modifica archivos bajo
  `source/StopWatch.Core/` o `source/StopWatch.Plugin.Abstractions/`
- **THEN** el proceso de release se evalúa para ese push

#### Scenario: El push solo toca archivos no relacionados
- **WHEN** un push a `main` o a una rama `rc*` solo modifica archivos fuera de
  `source/StopWatch/`, `source/StopWatch.Core/` y
  `source/StopWatch.Plugin.Abstractions/` (por ejemplo, `openspec/`,
  `CLAUDE.md`, o `.github/`)
- **THEN** el proceso de release no se evalúa y no se produce ningún release

## ADDED Requirements

### Requirement: Los tests corren en Windows y en Linux

La corrida de tests automatizados que condiciona un release SHALL
ejecutarse en Windows y en Linux, y SHALL considerarse aprobada solo si
pasa en ambas plataformas.

#### Scenario: Un test falla solo en Linux
- **WHEN** se hace push a `main` o a una rama `rc*` y un test falla en Linux
  pero pasa en Windows
- **THEN** no se produce ningún release para ese commit
