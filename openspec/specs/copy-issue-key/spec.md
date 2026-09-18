## Purpose

Permite copiar al portapapeles, con un solo click, el número del issue de
una fila o el número de su issue padre cuando corresponde, sin tener que
seleccionar texto a mano.
## Requirements
### Requirement: Copiar el key propio del issue

Cada fila de issue de la ventana principal SHALL ofrecer una acción para
copiar el key de ese issue al portapapeles del sistema, únicamente cuando
esa fila tenga un summary resuelto desde Jira - la misma condición que
`issue-open-action` exige para habilitar "abrir en el navegador".

#### Scenario: Usuario copia el key propio

- **WHEN** el usuario hace click en el ícono de copiar asociado al key del
  issue
- **THEN** el key del issue se copia al portapapeles del sistema

#### Scenario: Ícono de copiar el key propio no estorba cuando no se usa

- **WHEN** el mouse no está sobre la fila del issue
- **THEN** el ícono de copiar el key propio no es visible

#### Scenario: Issue sin resolver todavía

- **WHEN** el usuario tipeó o pegó una issue key en una fila de la ventana
  principal y todavía no se resolvió ningún summary para ella, y pasa el
  mouse sobre esa fila
- **THEN** el ícono de copiar el key propio no es visible en esa fila

### Requirement: Copiar el key del parent en subtareas

Cuando el issue de una fila es una subtarea y Jira devolvió el key de su
parent, la fila SHALL ofrecer una acción separada para copiar ese parent
key al portapapeles.

#### Scenario: Usuario copia el key del parent

- **WHEN** el issue de la fila es una subtarea con parent key disponible
- **AND** el usuario hace click en el ícono de copiar asociado al parent
- **THEN** el parent key se copia al portapapeles del sistema

#### Scenario: Issue no es subtarea

- **WHEN** el issue de la fila no es una subtarea
- **THEN** no se muestra ningún ícono de copiar el parent key en esa fila

#### Scenario: Issue es subtarea pero sin parent key disponible

- **WHEN** el issue de la fila es una subtarea y Jira no devolvió el key de
  su parent (por ejemplo, la request de summary falló)
- **THEN** no se muestra ningún ícono de copiar el parent key en esa fila

### Requirement: Confirmación visual de la copia

Al copiar un key mediante cualquiera de los íconos de esta capability, el
sistema SHALL confirmar visualmente la acción sin agregar texto adicional
a la fila.

#### Scenario: Ícono confirma la copia

- **WHEN** el usuario hace click en un ícono de copiar (propio o del
  parent) y la copia se realiza
- **THEN** ese ícono cambia temporalmente a un estado de confirmación
  (check) y luego vuelve a su apariencia normal

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

