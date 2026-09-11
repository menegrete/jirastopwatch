## Purpose

Permite copiar al portapapeles, con un solo click, el número del issue de
una fila o el número de su issue padre cuando corresponde, sin tener que
seleccionar texto a mano.

## ADDED Requirements

### Requirement: Copiar el key propio del issue

Cada fila de issue SHALL ofrecer una acción para copiar el key de ese issue
al portapapeles del sistema.

#### Scenario: Usuario copia el key propio

- **WHEN** el usuario hace click en el ícono de copiar asociado al key del
  issue
- **THEN** el key del issue se copia al portapapeles del sistema

#### Scenario: Ícono de copiar el key propio no estorba cuando no se usa

- **WHEN** el mouse no está sobre la fila del issue
- **THEN** el ícono de copiar el key propio no es visible

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

Esta capability SHALL aplicarse únicamente a las filas de issue de la
ventana principal, en sus dos densidades (compact y spacious).

#### Scenario: Mini timer no ofrece esta acción

- **WHEN** el usuario está viendo el mini timer flotante
- **THEN** no se ofrece ninguna acción de copiar el key propio ni el del
  parent
