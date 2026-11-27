# Spec Delta

## MODIFIED Requirements

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
