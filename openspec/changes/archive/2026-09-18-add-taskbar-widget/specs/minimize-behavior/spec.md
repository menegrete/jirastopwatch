## MODIFIED Requirements

### Requirement: El usuario elige adónde va la ventana principal al minimizarla

La ventana de Configuración SHALL ofrecer un control con tres opciones
excluyentes — `Mini View`, `Tray` y `Taskbar Widget` — para decidir qué
ocurre al minimizar la ventana principal con el control nativo de Windows.
En una instalación nueva, SHALL estar seleccionado `Mini View` por default.
La elección SHALL persistir entre reinicios de la aplicación. Este control
SHALL estar disponible únicamente en Windows, igual que hoy.

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
  minimizado y cierra la ventana de Configuración
- **THEN** la próxima vez que minimice la ventana principal aparece el
  widget de taskbar en vez de la vista mini o el ícono de bandeja

#### Scenario: El control no aparece fuera de Windows

- **WHEN** la aplicación corre en una plataforma donde el ícono de bandeja
  no está disponible
- **THEN** el control de minimizado (con sus tres opciones) no aparece en
  Configuración

## ADDED Requirements

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
