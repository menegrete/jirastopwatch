# issue-removal Specification

## Purpose
Define cómo se quita una fila de la lista de issues y cuándo la aplicación pide confirmación, para que el usuario no pierda sin darse cuenta tiempo que todavía no cargó a Jira.

## Requirements

### Requirement: Quitar una fila con tiempo pendiente pide confirmación

Cuando el usuario quita una fila que tiene tiempo pendiente de cargar, la aplicación SHALL pedir confirmación antes de eliminarla. Una fila tiene tiempo pendiente exactamente cuando el botón de cargar tiempo de esa fila está habilitado, esté su timer corriendo o en pausa. La confirmación SHALL aplicarse a todas las formas de quitar una fila: el botón de la fila, Ctrl+Delete y Delete en el campo del key.

#### Scenario: Se quita una fila con tiempo pendiente

- **WHEN** el usuario quita una fila cuyo botón de cargar tiempo está habilitado
- **THEN** la aplicación muestra una confirmación antes de eliminar la fila
- **AND** la fila sigue en la lista mientras la confirmación está abierta

#### Scenario: El usuario confirma

- **WHEN** el usuario confirma la eliminación en la ventana de confirmación
- **THEN** la fila se elimina junto con su tiempo acumulado

#### Scenario: El usuario cancela

- **WHEN** el usuario cancela la confirmación, o la cierra sin confirmar
- **THEN** la fila permanece en la lista sin cambios
- **AND** su timer conserva el estado y el tiempo que tenía

#### Scenario: El timer de la fila está corriendo

- **WHEN** el usuario quita una fila con tiempo pendiente cuyo timer está corriendo
- **THEN** la confirmación indica que el timer sigue en curso

#### Scenario: Se quita la fila con el atajo de teclado

- **WHEN** el usuario presiona Ctrl+Delete o Delete en el campo del key de una fila con tiempo pendiente
- **THEN** se muestra la misma confirmación que con el botón de la fila

### Requirement: Quitar una fila sin tiempo pendiente no pide confirmación

Cuando el usuario quita una fila que no tiene tiempo pendiente de cargar, la aplicación SHALL eliminarla de inmediato, sin confirmación.

#### Scenario: Se quita una fila sin tiempo acumulado

- **WHEN** el usuario quita una fila cuyo botón de cargar tiempo está deshabilitado
- **THEN** la fila se elimina de inmediato
- **AND** no se muestra ninguna ventana de confirmación

### Requirement: La confirmación informa qué se perdería y no elimina por defecto

La confirmación SHALL identificar la fila y el tiempo que se perdería. Sus controles SHALL ofrecer eliminar y cancelar, y la acción por defecto (Enter) y la de escape (Esc) SHALL ser cancelar. La ventana SHALL presentarse con los colores del tema activo.

#### Scenario: Contenido de la confirmación

- **WHEN** se muestra la confirmación para una fila con key y tiempo pendiente
- **THEN** el texto incluye el key del issue y el tiempo acumulado sin cargar

#### Scenario: La fila todavía no tiene key

- **WHEN** se muestra la confirmación para una fila sin key
- **THEN** el texto sigue siendo comprensible y muestra el tiempo acumulado

#### Scenario: El usuario presiona Enter o Esc

- **WHEN** la confirmación está abierta y el usuario presiona Enter o Esc
- **THEN** la fila no se elimina

#### Scenario: Tema activo

- **WHEN** la confirmación se abre con el tema oscuro o con el claro
- **THEN** se presenta con los colores de ese tema, sin elementos del otro tema
