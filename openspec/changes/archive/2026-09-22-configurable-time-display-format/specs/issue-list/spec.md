## ADDED Requirements

### Requirement: El usuario elige el formato de presentación del tiempo

La aplicación SHALL ofrecer al menos dos formatos de presentación para el
tiempo transcurrido que se muestra en la lista: notación Jira (por ejemplo
"2h 15m") y notación reloj (horas:minutos, por ejemplo "2:15"). El formato
elegido SHALL aplicarse tanto al tiempo transcurrido de cada fila como al
total de la lista, y SHALL persistir entre ejecuciones.

En notación reloj, el tiempo SHALL presentarse siempre con la parte de horas,
incluso cuando sea cero (por ejemplo "0:45"), y SHALL NO incluir segundos.

Elegir un formato SHALL ser puramente una cuestión de presentación: SHALL NO
alterar el valor que se postea a Jira como worklog, ni el estado de ningún
timer.

#### Scenario: El usuario elige notación reloj

- **WHEN** el usuario elige el formato reloj
- **THEN** cada fila muestra su tiempo transcurrido como horas:minutos (por
  ejemplo "2:15")
- **AND** el total de la lista se muestra con el mismo formato

#### Scenario: Tiempo transcurrido de menos de una hora en notación reloj

- **WHEN** el formato elegido es reloj y una fila tiene menos de una hora
  transcurrida
- **THEN** esa fila muestra la parte de horas en cero (por ejemplo "0:45")

#### Scenario: El usuario elige notación Jira

- **WHEN** el usuario elige el formato Jira
- **THEN** cada fila y el total de la lista se muestran como hoy (por ejemplo
  "2h 15m")

#### Scenario: El usuario reinicia la aplicación

- **WHEN** el usuario elige un formato, cierra la aplicación y la vuelve a
  abrir
- **THEN** la lista se presenta con el formato que había elegido

#### Scenario: El formato elegido no afecta el worklog posteado

- **WHEN** el usuario tiene elegido el formato reloj y postea un worklog a
  Jira
- **THEN** el tiempo posteado sigue expresado en notación Jira, sin importar
  el formato de presentación elegido

### Requirement: El diálogo de edición de tiempo sigue el formato elegido

El diálogo de edición de tiempo SHALL precargar su campo con el tiempo
transcurrido en el formato de presentación elegido. El campo SHALL aceptar
que el usuario tipee el valor en cualquiera de las dos notaciones (Jira o
reloj), sin importar cuál esté elegida como formato de presentación.

#### Scenario: Se abre el diálogo con formato reloj elegido

- **WHEN** el usuario abre el diálogo de edición de tiempo con el formato
  reloj elegido
- **THEN** el campo se precarga con el tiempo en notación reloj (por ejemplo
  "2:15")

#### Scenario: Se abre el diálogo con formato Jira elegido

- **WHEN** el usuario abre el diálogo de edición de tiempo con el formato
  Jira elegido
- **THEN** el campo se precarga con el tiempo en notación Jira (por ejemplo
  "2h 15m")

#### Scenario: El usuario tipea en la notación que no es la elegida

- **WHEN** el formato elegido es reloj y el usuario tipea un valor en
  notación Jira (por ejemplo "1h 30m"), o el formato elegido es Jira y tipea
  un valor en notación reloj (por ejemplo "1:30")
- **THEN** el valor se acepta y se interpreta correctamente
