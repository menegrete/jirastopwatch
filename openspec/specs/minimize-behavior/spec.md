## Purpose

Define adónde va la ventana principal cuando el usuario la minimiza con el
control nativo de Windows: a la vista mini, a un ícono en la bandeja del
sistema, o al widget de la barra de tareas, según un setting elegido en
Configuración.

## Requirements

### Requirement: El usuario elige adónde va la ventana principal al minimizarla

La ventana de Configuración SHALL ofrecer un control con opciones
excluyentes para decidir qué ocurre al minimizar la ventana principal con
el control nativo del sistema. En Windows, las opciones SHALL ser
`Mini View`, `Tray` y `Taskbar Widget`. En Linux, SHALL ser `Mini View` y
`Tray`. En una instalación nueva, SHALL estar seleccionado `Mini View` por
default. La elección SHALL persistir entre reinicios de la aplicación.

#### Scenario: Instalación nueva

- **WHEN** el usuario abre la aplicación por primera vez y va a Configuración
- **THEN** el control de minimizado muestra `Mini View` seleccionado

#### Scenario: El usuario cambia la opción

- **WHEN** el usuario selecciona `Tray` en el control de minimizado y cierra
  la ventana de Configuración
- **THEN** la próxima vez que minimice la ventana principal se aplica el
  comportamiento de `Tray`
- **AND** la elección sigue siendo `Tray` la próxima vez que se abre la
  aplicación

#### Scenario: El usuario selecciona Taskbar Widget

- **WHEN** el usuario selecciona `Taskbar Widget` en el control de
  minimizado en Windows y cierra la ventana de Configuración
- **THEN** la próxima vez que minimice la ventana principal aparece el
  widget de taskbar en vez de la vista mini o el ícono de bandeja

#### Scenario: El control no aparece fuera de Windows

- **WHEN** la aplicación corre en una plataforma distinta de Windows y de
  Linux, donde el ícono de bandeja no está disponible
- **THEN** el control de minimizado no aparece en Configuración

#### Scenario: El control en Linux

- **WHEN** el usuario abre Configuración en Linux
- **THEN** el control de minimizado ofrece `Mini View` y `Tray`, y no
  ofrece `Taskbar Widget`

#### Scenario: Minimizar a la bandeja en Linux

- **WHEN** el usuario tiene seleccionado `Tray` en Linux y minimiza la
  ventana principal
- **THEN** la ventana se esconde y aparece el ícono de la aplicación en el
  área de notificaciones del escritorio

### Requirement: Minimizar con la opción Mini View activa la vista mini

Con la opción `Mini View` seleccionada, minimizar la ventana principal con
el control nativo de Windows SHALL tener el mismo efecto que accionar el
control explícito de vista mini de la toolbar: la ventana principal SHALL
esconderse y la vista mini SHALL aparecer.

#### Scenario: El usuario minimiza con Mini View seleccionado

- **WHEN** el usuario minimiza la ventana principal con el control nativo de
  Windows, con la opción `Mini View` seleccionada
- **THEN** la ventana principal deja de estar visible
- **AND** aparece la vista mini
- **AND** los timers que estaban corriendo siguen corriendo

### Requirement: Minimizar con la opción Tray esconde la ventana y muestra un ícono en la bandeja

Con la opción `Tray` seleccionada, minimizar la ventana principal con el
control nativo de Windows SHALL esconderla de la barra de tareas y SHALL
mostrar un ícono en la bandeja del sistema en su lugar. Un click sobre ese
ícono SHALL restaurar la ventana principal.

#### Scenario: El usuario minimiza con Tray seleccionado

- **WHEN** el usuario minimiza la ventana principal con el control nativo de
  Windows, con la opción `Tray` seleccionada
- **THEN** la ventana principal deja de tener una entrada en la barra de
  tareas
- **AND** aparece un ícono en la bandeja del sistema

#### Scenario: El usuario restaura desde la bandeja

- **WHEN** el usuario hace click en el ícono de la bandeja
- **THEN** la ventana principal vuelve a mostrarse
- **AND** el ícono de la bandeja desaparece

### Requirement: Minimizar con la opción Taskbar Widget activa el widget de taskbar

Con la opción `Taskbar Widget` seleccionada, minimizar la ventana principal
con el control nativo de Windows SHALL esconder la ventana principal y
mostrar el widget de taskbar (capability `taskbar-widget-view`).

#### Scenario: El usuario minimiza con Taskbar Widget seleccionado

- **WHEN** el usuario minimiza la ventana principal con el control nativo de
  Windows, con la opción `Taskbar Widget` seleccionada
- **THEN** la ventana principal deja de estar visible
- **AND** aparece el widget de taskbar
- **AND** el timer que estaba corriendo sigue corriendo

### Requirement: Taskbar Widget es excluyente con permitir múltiples timers

Como el widget de taskbar solo tiene sentido mostrando un único timer
activo, la opción `Taskbar Widget` y la opción "permitir múltiples timers"
SHALL ser mutuamente excluyentes en Configuración: mientras `Taskbar Widget`
esté seleccionado en el control de minimizado, el control "permitir
múltiples timers" SHALL aparecer deshabilitado.

#### Scenario: Taskbar Widget está seleccionado

- **WHEN** el usuario selecciona `Taskbar Widget` en el control de
  minimizado
- **THEN** el control "permitir múltiples timers" aparece deshabilitado en
  Configuración

#### Scenario: El usuario cambia a otra opción de minimizado

- **WHEN** el usuario tenía `Taskbar Widget` seleccionado y lo cambia a
  `Mini View` o `Tray`
- **THEN** el control "permitir múltiples timers" vuelve a estar habilitado

### Requirement: La preferencia anterior de minimizado se migra a la opción equivalente

Al actualizar desde una versión donde el minimizado se controlaba con el
checkbox "Minimize to tray", la aplicación SHALL migrar ese valor al setting
nuevo la primera vez que se ejecuta luego de actualizar: "Minimize to tray"
activado SHALL mapear a `Tray`; desactivado SHALL mapear a `Mini View`.

#### Scenario: El usuario tenía "Minimize to tray" activado

- **WHEN** el usuario actualiza desde una versión con "Minimize to tray"
  activado
- **THEN** el control de minimizado en Configuración muestra `Tray`
  seleccionado
- **AND** minimizar la ventana principal sigue comportándose igual que antes
  de actualizar

#### Scenario: El usuario tenía "Minimize to tray" desactivado

- **WHEN** el usuario actualiza desde una versión con "Minimize to tray"
  desactivado
- **THEN** el control de minimizado en Configuración muestra `Mini View`
  seleccionado
- **AND** minimizar la ventana principal pasa a mostrar la vista mini en vez
  de minimizar a la taskbar
