## MODIFIED Requirements

### Requirement: El usuario entra y sale de la vista mini a voluntad

La ventana principal SHALL ofrecer un control explícito para activar la
vista mini, excepto cuando el setting de minimizado (capability
`minimize-behavior`) está en `Taskbar Widget`, caso en el que ese control
SHALL estar oculto — evita que el usuario termine viendo el mismo timer
duplicado en la vista mini y en el widget de taskbar a la vez. Minimizar la
ventana principal con el control nativo de Windows SHALL tener el mismo
efecto que ese control cuando el setting de minimizado está en `Mini View`.
Al activarla, la ventana principal SHALL esconderse y la vista mini SHALL
aparecer. La vista mini SHALL ofrecer un único control para volver,
flotante y superpuesto sobre el conjunto de filas, que no se repite por
fila y permanece en el mismo lugar sin importar cuántos timers se estén
mostrando. Un doble click sobre el fondo de la vista mini (fuera de
cualquier control) SHALL tener el mismo efecto que ese control.

Volver a la ventana completa SHALL esconder la vista mini y restaurar la
ventana principal con el tamaño que tenía antes de activarse la vista mini, en
una posición válida: si la posición que tenía cae dentro de alguna pantalla
conectada, SHALL restaurarse ahí exactamente; si no, SHALL ubicarse en una
posición visible de la pantalla principal.

Entrar o salir de la vista mini SHALL NO alterar el estado de ningún timer.

#### Scenario: El usuario activa la vista mini
- **WHEN** el usuario acciona el control de vista mini en la ventana principal
- **THEN** la ventana principal deja de estar visible
- **AND** aparece la vista mini
- **AND** los timers que estaban corriendo siguen corriendo

#### Scenario: El usuario minimiza la ventana principal con Mini View seleccionado
- **WHEN** el setting de minimizado está en `Mini View` y el usuario
  minimiza la ventana principal con el control nativo de Windows
- **THEN** ocurre lo mismo que si hubiera accionado el control de vista mini
  de la toolbar

#### Scenario: El usuario vuelve a la vista completa
- **WHEN** el usuario acciona el control de volver en la vista mini
- **THEN** la vista mini deja de estar visible
- **AND** la ventana principal reaparece en la misma posición y tamaño que tenía
  al activarse la vista mini

#### Scenario: El usuario vuelve a la vista completa con doble click
- **WHEN** el usuario hace doble click sobre el fondo de la vista mini, fuera
  de cualquier control
- **THEN** ocurre lo mismo que si hubiera accionado el control de volver

#### Scenario: La pantalla donde estaba la ventana principal ya no existe
- **WHEN** el usuario activó la vista mini con la ventana principal en una
  pantalla que se desconectó mientras la vista mini estaba en uso, y acciona
  el control de volver (o el doble click)
- **THEN** la ventana principal reaparece en una posición visible de la
  pantalla principal, en vez de en la posición guardada
- **AND** el usuario puede verla y operarla sin reiniciar la aplicación

#### Scenario: La aplicación arranca
- **WHEN** el usuario abre la aplicación
- **THEN** se presenta la ventana principal, nunca la vista mini, sin importar
  en qué vista estaba al cerrarla

#### Scenario: Taskbar Widget está seleccionado como minimize behavior
- **WHEN** el setting de minimizado (capability `minimize-behavior`) está en
  `Taskbar Widget`
- **THEN** el control explícito de vista mini de la toolbar no está visible
  en la ventana principal

#### Scenario: El usuario cambia el minimize behavior a Taskbar Widget
- **WHEN** el usuario cambia el setting de minimizado a `Taskbar Widget` en
  Configuración y cierra la ventana de Configuración
- **THEN** el control explícito de vista mini deja de estar visible en la
  ventana principal, sin necesidad de reiniciar la app

#### Scenario: El usuario cambia el minimize behavior desde Taskbar Widget
- **WHEN** el usuario tenía `Taskbar Widget` seleccionado y lo cambia a
  `Mini View` o `Tray` en Configuración, y cierra la ventana de Configuración
- **THEN** el control explícito de vista mini vuelve a estar visible en la
  ventana principal
