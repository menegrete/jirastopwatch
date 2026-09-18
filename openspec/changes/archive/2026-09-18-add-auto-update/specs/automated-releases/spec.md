## ADDED Requirements

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
