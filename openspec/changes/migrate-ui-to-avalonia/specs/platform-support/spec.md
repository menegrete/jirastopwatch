# Spec Delta

## Purpose

Define en qué sistemas operativos corre la aplicación y qué garantiza en cada uno: el servidor gráfico que usa en Linux, cómo protege la credencial de Jira y dónde conserva los datos del usuario.

## ADDED Requirements

### Requirement: Plataformas soportadas

La aplicación SHALL correr en Windows 10 y Windows 11 x64, en Ubuntu 24.04 LTS o posterior x64 y en Linux Mint 22 o posterior x64, con la misma funcionalidad en todas, salvo las excepciones que cada capability declara para una plataforma en particular.

#### Scenario: Arranque en Ubuntu

- **WHEN** el usuario ejecuta la aplicación en Ubuntu 24.04 LTS con la sesión por defecto
- **THEN** la ventana principal se muestra y el usuario puede agregar issues, iniciar timers y cargar worklogs igual que en Windows

#### Scenario: Arranque en Linux Mint

- **WHEN** el usuario ejecuta la aplicación en Linux Mint 22 con el escritorio Cinnamon
- **THEN** la ventana principal se muestra y el usuario puede agregar issues, iniciar timers y cargar worklogs igual que en Windows

### Requirement: En Linux la aplicación usa X11

En Linux, la aplicación SHALL presentar sus ventanas a través de X11. En una sesión Wayland, SHALL hacerlo a través de la capa de compatibilidad XWayland, de modo que la posición de las ventanas que la aplicación elige y la condición de "siempre encima" se respeten igual que en una sesión X11.

#### Scenario: Sesión Wayland en Ubuntu

- **WHEN** el usuario activa la vista mini en una sesión Wayland de Ubuntu y la suelta cerca del borde derecho de la pantalla
- **THEN** la vista mini se alinea a ese borde y permanece encima de las ventanas de otras aplicaciones

#### Scenario: Posición recordada en Linux

- **WHEN** el usuario cierra la aplicación con la vista mini en una esquina y vuelve a abrirla en Linux
- **THEN** la vista mini aparece en la misma posición

### Requirement: La credencial de Jira se guarda protegida

La aplicación SHALL guardar el token de la API de Jira cifrado o en un almacén de secretos del sistema operativo, de forma que no quede en texto plano en los archivos de configuración ni pueda leerlo otro usuario del mismo equipo. En Windows SHALL usarse un mecanismo que siga leyendo el token guardado por versiones anteriores. En Linux SHALL usarse el llavero del usuario.

#### Scenario: Token guardado en Linux

- **WHEN** el usuario ingresa su token en Configuración en Linux y lo guarda
- **THEN** el token no aparece en texto plano en ningún archivo de configuración de la aplicación
- **AND** queda almacenado en el llavero de la sesión del usuario

#### Scenario: Token guardado por una versión anterior en Windows

- **WHEN** el usuario actualiza en Windows desde una versión que ya tenía su token guardado
- **THEN** la aplicación se conecta a Jira sin pedirle que vuelva a ingresar el token

#### Scenario: Llavero no disponible en Linux

- **WHEN** la aplicación corre en Linux y el llavero del usuario no está disponible o está bloqueado
- **THEN** la aplicación informa que no pudo leer ni guardar el token y no lo escribe en texto plano

### Requirement: Los datos del usuario se guardan en su perfil

La aplicación SHALL guardar la configuración, la lista de issues y los datos de plugins en una ubicación propia del usuario dentro de su perfil, según la convención de cada sistema operativo, y SHALL conservarlos entre ejecuciones y actualizaciones.

#### Scenario: Reinicio en Linux

- **WHEN** el usuario agrega issues y cambia el tema en Linux, cierra la aplicación y la vuelve a abrir
- **THEN** la lista de issues y el tema elegido se conservan

#### Scenario: Dos usuarios del mismo equipo

- **WHEN** dos usuarios distintos usan la aplicación en el mismo equipo
- **THEN** cada uno ve solo su propia configuración y su propia lista de issues
