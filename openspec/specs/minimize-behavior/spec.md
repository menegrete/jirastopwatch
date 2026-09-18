## Purpose

Define adónde va la ventana principal cuando el usuario la minimiza con el
control nativo de Windows: a la vista mini o a un ícono en la bandeja del
sistema, según un setting elegido en Configuración.

## Requirements

### Requirement: El usuario elige adónde va la ventana principal al minimizarla

La ventana de Configuración SHALL ofrecer un control con dos opciones
excluyentes — `Mini View` y `Tray` — para decidir qué ocurre al minimizar la
ventana principal con el control nativo de Windows. En una instalación
nueva, SHALL estar seleccionado `Mini View` por default. La elección SHALL
persistir entre reinicios de la aplicación.

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
