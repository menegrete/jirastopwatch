## MODIFIED Requirements

### Requirement: Alcance limitado a la ventana principal

Copiar el key del parent SHALL estar disponible únicamente en las filas de
issue de la ventana principal, en sus dos densidades (compact y spacious).
La vista mini SHALL NO ofrecer esa acción, dado que no maneja la noción de
issue padre. Copiar el key propio del issue, en cambio, también está
disponible en la vista mini (ver "Copiar el key propio desde la vista
mini").

#### Scenario: Mini timer no ofrece esta acción

- **WHEN** el usuario está viendo el mini timer flotante
- **THEN** no se ofrece ninguna acción de copiar el key del parent

## ADDED Requirements

### Requirement: Copiar el key propio desde la vista mini

Cada fila de la vista mini SHALL ofrecer una acción para copiar el key de
ese issue al portapapeles del sistema, con el mismo efecto que la acción
equivalente de la ventana principal.

A diferencia de la ventana principal, donde el ícono aparece al pasar el
mouse por cualquier parte de la fila, en la vista mini SHALL aparecer
únicamente al pasar el mouse sobre el key de esa fila, no sobre el resto
de la fila (summary, tiempo o control de pausa/reanudar).

#### Scenario: Usuario copia el key desde la vista mini

- **WHEN** el usuario pasa el mouse sobre el key de una fila de la vista
  mini y hace click en el ícono de copiar que aparece
- **THEN** el key de ese issue se copia al portapapeles del sistema

#### Scenario: Ícono no estorba fuera del hover sobre la key

- **WHEN** el mouse no está sobre el key de una fila de la vista mini
- **THEN** el ícono de copiar no es visible en esa fila

#### Scenario: Hover sobre el resto de la fila no muestra el ícono

- **WHEN** el mouse está sobre el summary, el tiempo o el control de
  pausa/reanudar de una fila de la vista mini, pero no sobre su key
- **THEN** el ícono de copiar el key no es visible en esa fila
