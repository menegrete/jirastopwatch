# Spec Delta

## MODIFIED Requirements

### Requirement: Los elementos dibujados por el sistema acompañan el tema

Los elementos que dibuja el sistema operativo y no la aplicación —barra de título,
barras de desplazamiento, bordes de campos y glifos de casillas y radios— SHALL
presentarse en la gama del tema activo. Donde el sistema no lo permita, la
aplicación SHALL dibujar el elemento por su cuenta. Como excepción, en Linux la
barra de título de las ventanas con decoración nativa la dibuja el gestor de
ventanas del escritorio y SHALL seguir el tema del sistema, no el de la aplicación.

#### Scenario: La ventana principal tiene contenido desplazable

- **WHEN** la lista de issues excede el alto visible y aparece la barra de
  desplazamiento, con el tema oscuro activo
- **THEN** la barra no se presenta como una banda clara

#### Scenario: Se observa la barra de título de cada ventana

- **WHEN** el usuario abre cada ventana en Windows con el tema oscuro activo
- **THEN** la barra de título se presenta en oscuro, no en claro

#### Scenario: Barra de título en Linux

- **WHEN** el usuario abre la ventana principal en Linux con el tema claro
  de la aplicación y el escritorio en tema oscuro
- **THEN** la barra de título se presenta con el tema oscuro del escritorio
  y el contenido de la ventana con el tema claro de la aplicación

#### Scenario: Un formulario tiene casillas de verificación y radios

- **WHEN** el usuario abre el diálogo de settings con el tema oscuro activo
- **THEN** los glifos de las casillas no se presentan como cuadrados claros
- **AND** el estado marcado se distingue del no marcado
