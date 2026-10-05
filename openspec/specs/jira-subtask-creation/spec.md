## Purpose

Define cómo el cliente de Jira consulta los tipos de subtask de un proyecto y crea subtasks con campos estándar, informando con claridad cuando el proyecto exige algo más.

## Requirements

### Requirement: Tipos de subtask de un proyecto

El cliente de Jira SHALL permitir consultar los tipos de issue de tipo subtask
disponibles en un proyecto, devolviendo identificador y nombre de cada uno.

#### Scenario: Proyecto con varios tipos de subtask

- **WHEN** el proyecto tiene dos tipos de subtask habilitados
- **THEN** se devuelven ambos y ningún tipo que no sea subtask

#### Scenario: Proyecto sin tipos de subtask

- **WHEN** el proyecto no tiene ningún tipo de subtask habilitado
- **THEN** se devuelve una lista vacía, que no constituye un error

### Requirement: Creación de una subtask con campos estándar

El cliente de Jira SHALL permitir crear una subtask a partir de la key del
padre, un summary y el identificador de un tipo de subtask, enviando
únicamente campos estándar (proyecto, padre, summary y tipo). Si la creación
tiene éxito, SHALL devolver la key de la subtask creada.

#### Scenario: Creación exitosa

- **WHEN** se crea una subtask con padre, summary y tipo válidos
- **THEN** el resultado es exitoso y contiene la key de la subtask creada

#### Scenario: Proyecto con campos obligatorios adicionales

- **WHEN** Jira rechaza la creación porque el proyecto exige campos adicionales
- **THEN** el resultado es un fallo de validación cuyo mensaje nombra cada campo faltante junto con el mensaje de Jira, y no se crea ninguna subtask

#### Scenario: Tipo inválido para el proyecto

- **WHEN** el tipo indicado no es una subtask válida del proyecto
- **THEN** el resultado es un fallo de validación con el mensaje de Jira

#### Scenario: Sin permiso para crear

- **WHEN** el usuario no tiene permiso de crear issues en el proyecto
- **THEN** el resultado es un fallo distinguible como "prohibido", con el mensaje de Jira

#### Scenario: Padre inexistente

- **WHEN** el padre no existe o no es visible para el usuario
- **THEN** el resultado es un fallo distinguible como "no encontrado"
