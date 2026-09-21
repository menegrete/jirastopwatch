## MODIFIED Requirements

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
  aplicarse al reiniciar

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
