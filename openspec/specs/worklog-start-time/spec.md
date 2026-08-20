## Purpose

Define cómo se ingresan la fecha y la hora de inicio de un worklog en el diálogo de
envío: en qué formato se presentan, y qué garantías de visibilidad y de
independencia del idioma del sistema tienen esos campos.

## Requirements

### Requirement: Fecha y hora de inicio en campos separados

El diálogo de envío de worklog SHALL presentar la fecha de inicio y la hora de
inicio como dos campos independientes.

#### Scenario: El usuario abre el diálogo de envío de worklog

- **WHEN** el usuario abre el diálogo para enviar un worklog
- **THEN** hay un campo para la fecha de inicio y otro para la hora de inicio
- **AND** cada uno se puede editar sin afectar al otro

### Requirement: Formato de la fecha de inicio

El campo de fecha de inicio SHALL presentar el valor como `dd/MM/yyyy`, con día y
mes de dos dígitos y año de cuatro.

#### Scenario: Se muestra una fecha de un solo dígito en día o mes

- **WHEN** la fecha de inicio es el 5 de marzo de 2026
- **THEN** el campo muestra `05/03/2026`

#### Scenario: El idioma del sistema no es español ni inglés

- **WHEN** el sistema operativo está configurado en cualquier idioma o región
- **THEN** el campo sigue mostrando la fecha como `dd/MM/yyyy`
- **AND** el orden de día, mes y año no cambia según la configuración regional

### Requirement: Formato de la hora de inicio

El campo de hora de inicio SHALL presentar el valor como `HH:mm`, en formato de 24
horas con dos dígitos para la hora.

#### Scenario: Se muestra una hora de la tarde

- **WHEN** la hora de inicio es 14:05
- **THEN** el campo muestra `14:05`
- **AND** no aparece ningún indicador AM/PM

#### Scenario: Se muestra una hora antes del mediodía

- **WHEN** la hora de inicio es 09:04
- **THEN** el campo muestra `09:04`

### Requirement: El contenido de los campos se muestra completo

El valor de cada campo SHALL ser visible en su totalidad. Ningún campo SHALL
recortar su contenido.

#### Scenario: Se observan los campos de fecha y hora

- **WHEN** el usuario abre el diálogo de envío de worklog
- **THEN** el campo de fecha muestra los diez caracteres de la fecha completa
- **AND** el campo de hora muestra los cinco caracteres de la hora completa
- **AND** ninguno de los dos queda truncado

### Requirement: Los valores de inicio siguen siendo editables

El usuario SHALL poder modificar la fecha y la hora de inicio antes de enviar el
worklog, y el valor enviado SHALL ser el que quedó en los campos.

#### Scenario: El usuario ajusta la hora de inicio antes de enviar

- **WHEN** el usuario modifica la hora de inicio y confirma el envío
- **THEN** el worklog se registra con la hora que quedó en el campo

#### Scenario: El usuario cancela el diálogo

- **WHEN** el usuario modifica la fecha o la hora y luego cancela
- **THEN** no se registra ningún worklog
