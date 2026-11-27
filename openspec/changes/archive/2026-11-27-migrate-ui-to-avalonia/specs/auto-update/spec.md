# Spec Delta

## MODIFIED Requirements

### Requirement: La descarga corresponde a la variante que está corriendo

Cuando hay una versión más nueva y el chequeo de actualizaciones está
activado, la app SHALL descargar únicamente el artifact de release que
corresponde a la plataforma (Windows o Linux) y a la variante
(self-contained o framework-dependent) que está ejecutando actualmente, y
SHALL verificar la descarga contra su checksum SHA256 publicado antes de
darla por válida.

#### Scenario: Corriendo la variante self-contained
- **WHEN** la app en ejecución es la variante self-contained y hay una
  versión más nueva
- **THEN** la app descarga el artifact self-contained de esa versión

#### Scenario: Corriendo la variante framework-dependent
- **WHEN** la app en ejecución es la variante framework-dependent y hay una
  versión más nueva
- **THEN** la app descarga el artifact framework-dependent de esa versión

#### Scenario: Corriendo en Linux
- **WHEN** la app en ejecución es la variante self-contained de Linux y hay
  una versión más nueva
- **THEN** la app descarga el `.tar.gz` self-contained de Linux de esa
  versión, y no ninguno de los artifacts de Windows

#### Scenario: El checksum no coincide
- **WHEN** el archivo descargado no coincide con su checksum SHA256
  publicado
- **THEN** la app descarta la descarga, no queda ninguna actualización
  pendiente de aplicar, y la app sigue funcionando con la versión actual

## ADDED Requirements

### Requirement: La actualización se aplica también en Linux

En Linux, la app SHALL aplicar una actualización verificada con el mismo
comportamiento que en Windows: al próximo cierre normal, sin dejar la
instalación a medio reemplazar, y volviendo a abrir la nueva versión. El
ejecutable instalado SHALL conservar el permiso de ejecución.

#### Scenario: Cierre normal con una actualización lista en Linux
- **WHEN** el usuario cierra la app en Linux y hay una actualización
  verificada y lista para aplicar
- **THEN** la nueva versión reemplaza a la instalación actual, la app se
  vuelve a abrir en la nueva versión y su ejecutable se puede volver a
  lanzar desde el escritorio

#### Scenario: Directorio de instalación no escribible en Linux
- **WHEN** la app está instalada en Linux en un directorio sobre el que el
  usuario no tiene permiso de escritura
- **THEN** la instalación actual queda intacta y la app sigue arrancando
  normalmente en la versión anterior

### Requirement: Actualizar desde la versión WPF conserva los datos del usuario

Al actualizarse en Windows desde una versión 3.x a la primera versión
multiplataforma, la app SHALL conservar la configuración, el token de Jira,
la lista de issues con sus tiempos acumulados, las posiciones y tamaños de
ventana recordados y los plugins instalados.

#### Scenario: Actualización automática de 3.x a 4.0
- **WHEN** una instalación 3.x en Windows se actualiza sola a la primera
  versión multiplataforma y se vuelve a abrir
- **THEN** el usuario ve los mismos issues con los mismos tiempos, el mismo
  tema y la misma configuración, y la app se conecta a Jira sin pedir el
  token

#### Scenario: Plugins 1.x tras la actualización
- **WHEN** la instalación actualizada tiene plugins compilados contra el
  contrato 1.x
- **THEN** los plugins siguen en su carpeta, se informan como no cargados
  por versión de contrato incompatible y la app funciona normalmente
