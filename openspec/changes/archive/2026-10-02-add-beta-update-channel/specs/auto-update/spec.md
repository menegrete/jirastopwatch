## ADDED Requirements

### Requirement: El usuario puede suscribirse a las versiones beta

La app SHALL exponer un ajuste "Subscribe to beta releases" que habilita
recibir pre-releases por el auto-update. Este ajuste SHALL estar
desactivado por defecto y SHALL persistirse entre sesiones.

#### Scenario: El ajuste está desactivado (por defecto)
- **WHEN** el usuario no cambió el ajuste de suscripción a betas
- **THEN** la app solo considera releases estables al chequear
  actualizaciones, exactamente como antes de existir el ajuste

#### Scenario: El usuario activa el ajuste
- **WHEN** el usuario activa la suscripción a betas en la configuración
- **THEN** a partir del siguiente chequeo la app considera releases
  estables y pre-releases

#### Scenario: El chequeo general está desactivado
- **WHEN** el chequeo de actualizaciones está desactivado, con la
  suscripción a betas activada o no
- **THEN** la app no realiza ninguna actividad de red de actualización

### Requirement: Con la suscripción activa se ofrece la versión más alta según semver

Con la suscripción a betas activada, la app SHALL elegir, entre todos los
releases publicados (estables y pre-releases, excluyendo borradores), el de
mayor versión según el orden de semver, donde una versión con sufijo de
prerelease es menor que la misma versión sin sufijo, y los identificadores
numéricos del sufijo se comparan como números.

#### Scenario: Hay un beta más nuevo que la versión actual
- **WHEN** la app corre `3.7.0` con la suscripción activa y el release más
  alto publicado es la pre-release `3.8.0-rc.1`
- **THEN** la app comienza la descarga y verificación de `3.8.0-rc.1`

#### Scenario: Un beta posterior reemplaza a uno anterior
- **WHEN** la app corre `3.8.0-rc.2` con la suscripción activa y hay
  publicada `3.8.0-rc.10`
- **THEN** la app ofrece `3.8.0-rc.10`, porque `rc.10` es mayor que `rc.2`

#### Scenario: La estable gana sobre sus betas
- **WHEN** están publicadas `3.8.0-rc.3` y `3.8.0`, con la suscripción activa
- **THEN** la versión candidata es `3.8.0`

#### Scenario: Con la suscripción desactivada se ignoran los betas
- **WHEN** hay publicada una pre-release `3.8.0-rc.1` más alta que el último
  release estable y la suscripción está desactivada
- **THEN** la app no ofrece ni descarga esa pre-release

### Requirement: Solo se ofrece una versión estrictamente mayor que la actual

La app SHALL ofrecer únicamente una versión estrictamente mayor, según
semver, que la que está corriendo, independientemente de si es estable o
beta y del estado del ajuste de suscripción, y SHALL NOT bajar de versión
ni ofrecer volver a un release estable anterior.

#### Scenario: Se apaga la suscripción estando en un beta
- **WHEN** la app corre `3.8.0-rc.1`, la suscripción está desactivada y el
  último release estable es `3.7.0`
- **THEN** la app no ofrece ninguna actualización

#### Scenario: Llega una estable mayor tras apagar la suscripción
- **WHEN** la app corre `3.8.0-rc.1`, la suscripción está desactivada y se
  publica el release estable `3.8.0`
- **THEN** la app ofrece `3.8.0`

#### Scenario: La versión actual es un beta con metadata de build
- **WHEN** la versión en ejecución incluye metadata de build (por ejemplo
  `3.8.0-rc.1+abc123`)
- **THEN** la metadata se ignora al comparar y la versión se trata como
  `3.8.0-rc.1`

### Requirement: La descarga de un beta usa los artifacts de esa versión completa

Al actualizar a una pre-release, la app SHALL buscar y descargar los
artifacts cuyo nombre incluye la versión completa con su sufijo de
prerelease, y SHALL verificarlos contra su checksum SHA256 igual que en un
release estable.

#### Scenario: Descarga de un beta
- **WHEN** la app va a actualizarse a `3.8.0-rc.1`
- **THEN** descarga y verifica `JiraStopWatch-v3.8.0-rc.1-<variante>` y su
  `.sha256`, correspondientes a la variante en ejecución

#### Scenario: El release beta no tiene los artifacts esperados
- **WHEN** el release de la versión candidata no contiene el artifact o el
  checksum esperados
- **THEN** la app no deja ninguna actualización pendiente y sigue
  funcionando con la versión actual

### Requirement: El aviso de actualización lista indica si es un beta

Cuando la actualización lista para aplicar es una pre-release, el aviso de
actualización SHALL indicar que se trata de una versión beta.

#### Scenario: La actualización pendiente es un beta
- **WHEN** hay staged una actualización a `3.8.0-rc.1`
- **THEN** el aviso muestra la versión completa e indica que es beta, además
  del link "What's new" hacia la página de ese release

#### Scenario: La actualización pendiente es estable
- **WHEN** hay staged una actualización a un release estable
- **THEN** el aviso no menciona beta
