## ADDED Requirements

### Requirement: Resultado tipado al registrar un worklog

Al registrar un worklog en Jira, el cliente SHALL devolver un resultado que
indique si tuvo éxito y, en ese caso, el identificador del worklog creado. Si
falla, el resultado SHALL incluir una razón estructurada y el mensaje de Jira.

#### Scenario: Worklog registrado

- **WHEN** Jira acepta el worklog
- **THEN** el resultado es exitoso y contiene el id del worklog creado

#### Scenario: Fallo con razón distinguible

- **WHEN** el registro falla
- **THEN** la razón es una de: no autorizado, prohibido, no encontrado, validación (por ejemplo issue cerrado), red o desconocida, según la respuesta de Jira, con su mensaje

#### Scenario: Sin credenciales

- **WHEN** no hay credenciales configuradas
- **THEN** el resultado es un fallo con razón "no autorizado"

### Requirement: Resultado tipado al registrar un comentario

Al registrar un comentario en Jira, el cliente SHALL devolver un resultado con
éxito o una razón estructurada y el mensaje de Jira, con las mismas razones que
el registro de worklogs.

#### Scenario: Comentario registrado

- **WHEN** Jira acepta el comentario
- **THEN** el resultado es exitoso

#### Scenario: Comentario rechazado

- **WHEN** Jira rechaza el comentario por permisos
- **THEN** el resultado es un fallo con razón "prohibido" y el mensaje de Jira

### Requirement: Sin cambio de comportamiento para el usuario final

La carga de tiempo desde la aplicación SHALL comportarse igual que antes de
este cambio: el timer de una fila solo se reinicia si todo lo que debía
registrarse (comentario y worklog) se registró con éxito.

#### Scenario: Falla el comentario

- **WHEN** el comentario falla y por configuración debe registrarse antes del worklog
- **THEN** no se registra el worklog y el timer no se reinicia

#### Scenario: Falla el worklog

- **WHEN** el worklog falla
- **THEN** el timer no se reinicia

#### Scenario: Todo se registra

- **WHEN** comentario y worklog se registran con éxito
- **THEN** el timer se reinicia
