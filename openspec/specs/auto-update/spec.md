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
