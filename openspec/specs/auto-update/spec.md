## Purpose

Lets the app detect, fetch, verify and install a newer released version on
its own, so users stop having to notice and manually re-download new
releases.

## Requirements

### Requirement: La app chequea si hay una versión más nueva disponible

Al iniciar, y luego periódicamente cada 1 hora mientras sigue corriendo, la
app SHALL consultar si existe una versión de Jira StopWatch más nueva que
la que está corriendo, sin bloquear la interfaz de usuario mientras lo
hace.

#### Scenario: Hay una versión más nueva
- **WHEN** la app inicia, o pasó 1 hora desde el último chequeo mientras
  sigue corriendo, y la versión más reciente publicada es mayor a
  `AppInfo.Version`
- **THEN** la app comienza el proceso de descarga y verificación de esa
  versión en segundo plano

#### Scenario: No hay una versión más nueva
- **WHEN** la app inicia, o pasó 1 hora desde el último chequeo mientras
  sigue corriendo, y la versión más reciente publicada es igual o anterior
  a `AppInfo.Version`
- **THEN** la app no descarga nada y continúa funcionando normalmente

#### Scenario: El chequeo falla
- **WHEN** el chequeo de la versión más nueva falla (sin red, error del
  servicio, límite de tasa alcanzado)
- **THEN** la app continúa funcionando normalmente, sin mostrar ningún
  error al usuario, y vuelve a intentar el chequeo en el próximo chequeo
  periódico (o, si la app se cierra antes, en el próximo inicio)

#### Scenario: Ya hay una actualización lista para aplicar
- **WHEN** pasó 1 hora desde el último chequeo mientras la app sigue
  corriendo, pero ya existe una actualización staged pendiente de aplicar
- **THEN** la app no vuelve a chequear ni a descargar nada hasta que esa
  actualización se aplique (o el usuario la descarte reiniciando sin
  aplicarla)

### Requirement: El usuario puede desactivar el chequeo de actualizaciones

La app SHALL exponer un ajuste que, al estar desactivado, evita todo
chequeo, descarga o aplicación automática de actualizaciones. Este ajuste
SHALL estar activado por defecto.

#### Scenario: El ajuste está activado (por defecto)
- **WHEN** el usuario no cambió el ajuste de chequeo de actualizaciones
- **THEN** la app chequea actualizaciones normalmente al iniciar

#### Scenario: El usuario desactiva el ajuste
- **WHEN** el usuario desactiva el chequeo de actualizaciones en la
  configuración
- **THEN** la app no vuelve a chequear, descargar ni aplicar
  actualizaciones hasta que el usuario reactive el ajuste

### Requirement: La descarga corresponde a la variante que está corriendo

Cuando hay una versión más nueva y el chequeo de actualizaciones está
activado, la app SHALL descargar únicamente el artifact de release que
corresponde a la variante que está ejecutando actualmente (self-contained o
framework-dependent), y SHALL verificar la descarga contra su checksum
SHA256 publicado antes de darla por válida.

#### Scenario: Corriendo la variante self-contained
- **WHEN** la app en ejecución es la variante self-contained y hay una
  versión más nueva
- **THEN** la app descarga el artifact self-contained de esa versión

#### Scenario: Corriendo la variante framework-dependent
- **WHEN** la app en ejecución es la variante framework-dependent y hay una
  versión más nueva
- **THEN** la app descarga el artifact framework-dependent de esa versión

#### Scenario: El checksum no coincide
- **WHEN** el archivo descargado no coincide con su checksum SHA256
  publicado
- **THEN** la app descarta la descarga, no queda ninguna actualización
  pendiente de aplicar, y la app sigue funcionando con la versión actual

### Requirement: La actualización se aplica sin interrumpir una sesión en curso

La app SHALL dejar la actualización verificada lista para aplicar sin
modificar los archivos de instalación mientras la app está corriendo, y
SHALL aplicarla recién en el próximo cierre normal de la app (nunca
forzando el cierre de la app ni interrumpiendo un timer en ejecución para
instalarla).

#### Scenario: Actualización lista mientras la app sigue en uso
- **WHEN** una actualización terminó de descargarse y verificarse
- **THEN** la app sigue funcionando con la versión actual sin interrupción,
  y muestra en rojo y negrita que hay una actualización lista para
  aplicarse al reiniciar, junto con un link "What's new" hacia la página
  del release en GitHub correspondiente a esa versión

#### Scenario: El usuario abre las notas de la versión desde el aviso
- **WHEN** el usuario hace clic en el link "What's new" del aviso de
  actualización lista
- **THEN** la app abre la página de ese release en GitHub en el navegador
  por defecto del usuario, sin afectar la actualización staged ni la
  sesión en curso

#### Scenario: El usuario cierra la app normalmente con una actualización lista
- **WHEN** el usuario cierra la app y hay una actualización verificada y
  lista para aplicar
- **THEN** la nueva versión reemplaza a la instalación actual y la app se
  vuelve a abrir automáticamente en la nueva versión

#### Scenario: La app se cierra sin que la actualización se aplique
- **WHEN** el proceso de la app termina de una forma que el proceso de
  aplicación no llega a detectar (timeout esperando el cierre)
- **THEN** la instalación actual queda intacta y sin aplicar, y la
  actualización se reintenta en un próximo cierre

### Requirement: Una aplicación fallida no deja la instalación rota

Si el directorio de instalación no se puede escribir, o cualquier paso de
la descarga, verificación o aplicación falla, la app SHALL conservar la
instalación actual funcionando exactamente como estaba, sin dejar archivos
a medio reemplazar.

#### Scenario: El directorio de instalación no es escribible
- **WHEN** la app intenta aplicar una actualización verificada pero no
  tiene permisos de escritura sobre su directorio de instalación
- **THEN** la instalación actual queda intacta, y la app sigue arrancando
  normalmente en la versión anterior

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

### Requirement: Updates preserve installed plugins
Applying an update SHALL NOT remove, replace or modify installed plugins, regardless of whether the plugins live next to the executable or in the per-user data folder.

#### Scenario: Update with plugins installed
- **WHEN** an update is applied on a machine with plugins installed
- **THEN** the same plugin folders and files exist after the update and load on next start
