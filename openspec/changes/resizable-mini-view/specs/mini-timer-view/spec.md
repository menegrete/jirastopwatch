## ADDED Requirements

### Requirement: El usuario cambia el ancho de la vista mini arrastrando sus bordes

La vista mini SHALL permitir cambiar su ancho arrastrando con el mouse su borde
izquierdo o su borde derecho. Mientras el mouse está sobre uno de esos bordes,
SHALL mostrarse el cursor de redimensionado horizontal. El alto de la vista
mini SHALL seguir determinado únicamente por la cantidad de filas. El ancho
SHALL mantenerse dentro de un mínimo que deje completamente visibles, en cada
fila, el key, el tiempo y el control de pausa/reanudación, y el control para
volver a la vista completa; y dentro de un máximo igual al ancho del área de
trabajo de la pantalla donde se encuentra.

#### Scenario: El usuario ensancha la vista mini desde el borde derecho

- **WHEN** el usuario arrastra el borde derecho de la vista mini hacia la
  derecha
- **THEN** la vista mini se ensancha y su borde izquierdo no se mueve
- **AND** el summary de cada fila muestra más texto antes de truncarse

#### Scenario: El usuario ensancha la vista mini desde el borde izquierdo

- **WHEN** el usuario arrastra el borde izquierdo de la vista mini hacia la
  izquierda
- **THEN** la vista mini se ensancha y su borde derecho no se mueve

#### Scenario: El usuario achica la vista mini más allá del mínimo

- **WHEN** el usuario arrastra un borde hacia adentro más allá del ancho mínimo
- **THEN** la vista mini se queda en el ancho mínimo
- **AND** el key, el tiempo y los controles de cada fila siguen completamente
  visibles

#### Scenario: El usuario ensancha la vista mini más allá de la pantalla

- **WHEN** el usuario arrastra un borde hacia afuera más allá del ancho del área
  de trabajo de la pantalla
- **THEN** la vista mini se queda en el ancho del área de trabajo

#### Scenario: El usuario redimensiona con varias filas

- **WHEN** la vista mini muestra varias filas y el usuario cambia su ancho
- **THEN** todas las filas adoptan el nuevo ancho
- **AND** el alto no cambia

#### Scenario: El usuario arrastra desde el centro tras redimensionar

- **WHEN** el usuario cambió el ancho y luego arrastra la vista mini desde una
  superficie que no es un control ni un borde
- **THEN** la vista mini se mueve sin cambiar de ancho

### Requirement: El ancho de la vista mini persiste y siempre es utilizable

El ancho elegido para la vista mini SHALL conservarse entre ejecuciones de la
aplicación. Si el usuario nunca lo cambió, SHALL usarse el ancho por defecto.
Antes de presentar la vista mini, la aplicación SHALL ajustar el ancho guardado
al mínimo y al máximo vigentes en la pantalla donde va a aparecer.

#### Scenario: El usuario reabre la aplicación

- **WHEN** el usuario cambió el ancho de la vista mini, cerró la aplicación y la
  vuelve a abrir y a activar la vista mini
- **THEN** la vista mini aparece con ese mismo ancho

#### Scenario: Nunca se cambió el ancho

- **WHEN** el usuario activa la vista mini sin haberla redimensionado nunca
- **THEN** la vista mini aparece con el ancho por defecto

#### Scenario: El ancho guardado ya no cabe en la pantalla

- **WHEN** el ancho guardado es mayor que el área de trabajo de la pantalla
  donde aparece la vista mini, por ejemplo tras cambiar de monitor o de
  resolución
- **THEN** la vista mini aparece con el ancho máximo que cabe en esa pantalla
