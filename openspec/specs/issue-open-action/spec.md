## Purpose

Rige cuándo está disponible la acción "abrir issue en el navegador" de una fila, de forma que el usuario solo pueda dispararla una vez que la key de esa fila haya sido confirmada contra Jira, y que esa disponibilidad siga a la key actual en vez de a una anterior.

## Requirements

### Requirement: La acción de abrir requiere una issue confirmada por Jira

El sistema SHALL habilitar la acción "abrir en el navegador" de una fila únicamente cuando esa fila tenga un summary no vacío resuelto desde Jira para su issue key actual. Una key no vacía por sí sola SHALL NOT ser suficiente.

#### Scenario: Key tecleada pero todavía no resuelta

- **WHEN** el usuario teclea o pega una issue key en una fila y todavía no se resolvió ningún summary para ella
- **THEN** la acción "abrir en el navegador" de la fila está deshabilitada

#### Scenario: La key resuelve a una issue existente

- **WHEN** la issue key de la fila resuelve a un summary devuelto por Jira
- **THEN** la acción "abrir en el navegador" de la fila pasa a estar habilitada

#### Scenario: La key no existe en Jira

- **WHEN** el usuario ingresa una issue key que Jira reporta como inexistente
- **THEN** la acción "abrir en el navegador" de la fila permanece deshabilitada

### Requirement: La disponibilidad de la acción de abrir sigue a la key actual

El sistema SHALL actualizar la disponibilidad de la acción "abrir en el navegador" de una fila inmediatamente cada vez que cambien la issue key o el summary resuelto de esa fila, sin requerir una acción no relacionada para forzar una actualización.

#### Scenario: Se cambia una key ya resuelta

- **WHEN** una fila ya tiene un summary resuelto y su acción "abrir en el navegador" habilitada, y el usuario cambia la issue key a un valor distinto
- **THEN** la acción "abrir en el navegador" de la fila pasa a estar deshabilitada inmediatamente, antes de que resuelva el summary de la nueva key

#### Scenario: La nueva key resuelve después de haber cambiado

- **WHEN** la issue key de una fila acaba de cambiar y luego resuelve a un nuevo summary desde Jira
- **THEN** la acción "abrir en el navegador" de la fila pasa a estar habilitada para la nueva key
