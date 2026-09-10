## ADDED Requirements

### Requirement: Los cambios de estado se presentan con una transición

Cuando un elemento de la interfaz cambia de color para comunicar un cambio de
estado —un timer que arranca o se pausa, una fila que pasa a ser la
seleccionada—, el cambio SHALL presentarse como una transición y no como un
salto instantáneo.

La transición SHALL terminar en un color del tema activo. SHALL ser lo
suficientemente breve como para no retrasar la lectura del estado nuevo, y
SHALL NO ser el único portador del estado: el estado SHALL seguir siendo
distinguible una vez que la transición terminó, y también mientras está en
curso.

#### Scenario: El usuario arranca un timer

- **WHEN** el usuario inicia el timer de un issue
- **THEN** el campo de tiempo llega a su color de timer corriendo de forma
  gradual
- **AND** el color de llegada pertenece al tema activo

#### Scenario: El usuario pausa un timer

- **WHEN** el usuario pausa un timer que estaba corriendo
- **THEN** el campo de tiempo vuelve gradualmente a su color de timer detenido
- **AND** una vez terminada la transición se distingue de un timer corriendo

#### Scenario: El usuario cambia de issue seleccionado rápidamente

- **WHEN** el usuario selecciona una fila y enseguida otra, antes de que
  termine la transición de la primera
- **THEN** la fila que queda seleccionada se presenta como seleccionada
- **AND** la anterior se presenta como no seleccionada
- **AND** ninguna de las dos queda en un color intermedio

#### Scenario: El usuario cambia el tema durante una transición

- **WHEN** el tema cambia mientras una transición de estado está en curso
- **THEN** al terminar, el elemento presenta el color del tema nuevo
