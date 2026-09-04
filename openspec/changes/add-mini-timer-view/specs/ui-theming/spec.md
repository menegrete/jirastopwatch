## MODIFIED Requirements

### Requirement: Superficie consistente en toda la aplicación

Todas las ventanas de la aplicación SHALL presentarse con los colores del tema
activo, sin importar con qué tecnología de interfaz esté construida cada una.
Ningún panel, campo de entrada, borde o separador SHALL quedar con los colores
del otro tema.

#### Scenario: Se abren todas las ventanas con el tema oscuro activo

- **WHEN** el usuario abre la ventana principal y luego cada diálogo (settings,
  submit worklog, edit timer, about) y activa la vista mini
- **THEN** todas presentan fondo oscuro y texto claro
- **AND** ningún elemento queda como un rectángulo claro sobre el fondo oscuro

#### Scenario: Se abren todas las ventanas con el tema claro activo

- **WHEN** el usuario abre la ventana principal y luego cada diálogo y activa la
  vista mini
- **THEN** todas presentan fondo claro y texto oscuro
- **AND** ningún elemento queda como un rectángulo oscuro sobre el fondo claro

#### Scenario: Un campo de entrada queda deshabilitado

- **WHEN** un campo de texto pasa a estado deshabilitado
- **THEN** se distingue visualmente de un campo habilitado
- **AND** sigue perteneciendo a la gama del tema activo

#### Scenario: El usuario cambia de tema con la vista mini visible

- **WHEN** el usuario cambia el tema y luego activa la vista mini
- **THEN** la vista mini se presenta con los colores del tema recién elegido
