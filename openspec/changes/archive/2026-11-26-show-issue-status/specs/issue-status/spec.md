# Spec Delta

## Purpose

Define cómo se presenta el estado de Jira de cada issue en la ventana principal y en la vista mini, y cuándo se actualiza, para que el usuario sepa en qué punto del flujo está cada issue sin abrirla en el navegador.

## ADDED Requirements

### Requirement: Cada fila muestra el estado de su issue

Cada fila de la ventana principal SHALL mostrar un indicador del estado de Jira de su issue, entre el control de abrir en el navegador y el summary, en cualquier densidad de la lista. Cada fila de la vista mini SHALL mostrar el mismo indicador entre el key y el summary. El indicador SHALL ocupar un ancho fijo y pequeño, igual en todas las filas, sin texto visible.

#### Scenario: Una fila con estado resuelto en la ventana principal

- **WHEN** Jira resolvió el estado del issue de una fila
- **THEN** la fila muestra el indicador de estado entre el control de abrir en el navegador y el summary

#### Scenario: Una fila con estado resuelto en la vista mini

- **WHEN** la vista mini muestra una fila cuyo issue tiene estado resuelto
- **THEN** la fila muestra el indicador de estado entre el key y el summary

#### Scenario: El indicador no le quita espacio variable al summary

- **WHEN** dos issues de la lista tienen estados con nombres de distinto largo
- **THEN** el indicador ocupa el mismo ancho en las dos filas
- **AND** el summary de cada fila dispone del mismo ancho que la otra

### Requirement: El indicador representa la categoría del estado

El indicador SHALL representar la categoría del estado en Jira: por hacer, en curso o terminado. Cada categoría SHALL tener una forma y un color propios, y las tres SHALL distinguirse entre sí por la forma aunque no se perciba el color. Un estado de categoría desconocida SHALL presentarse como por hacer.

#### Scenario: Las tres categorías se distinguen sin color

- **WHEN** se observan los indicadores de un issue por hacer, uno en curso y uno terminado, en escala de grises
- **THEN** los tres indicadores se distinguen entre sí por su forma

#### Scenario: Estados distintos de la misma categoría

- **WHEN** dos issues están en estados de nombre distinto que Jira clasifica en la misma categoría, como "In Progress" y "Code Review"
- **THEN** las dos filas muestran el mismo indicador

#### Scenario: Categoría desconocida

- **WHEN** Jira devuelve un estado sin categoría o con una categoría que la aplicación no reconoce
- **THEN** el indicador se presenta como el de por hacer

### Requirement: El nombre del estado está disponible desde el indicador

Al posar el puntero sobre el indicador, la aplicación SHALL mostrar el nombre del estado tal como lo devuelve Jira.

#### Scenario: El usuario consulta el nombre del estado

- **WHEN** el usuario posa el puntero sobre el indicador de una issue en estado "Code Review"
- **THEN** la aplicación muestra "Code Review"

### Requirement: Sin estado resuelto no hay indicador

Una fila sin key, o cuyo estado todavía no se resolvió, SHALL presentarse sin indicador, conservando la posición del summary que tendría con indicador. Si Jira rechaza la consulta, la fila SHALL conservar el estado que ya mostraba.

#### Scenario: Una fila sin key

- **WHEN** una fila no tiene key
- **THEN** la fila no muestra indicador de estado

#### Scenario: Una fila recién agregada

- **WHEN** el usuario ingresa un key y Jira todavía no respondió
- **THEN** la fila no muestra indicador
- **AND** el indicador aparece cuando llega el estado, sin desplazar el summary

#### Scenario: Jira rechaza la consulta

- **WHEN** una fila ya muestra un estado y la siguiente consulta a Jira es rechazada
- **THEN** la fila conserva el indicador y el nombre de estado que tenía

### Requirement: El estado se actualiza junto con el summary

El estado de cada fila SHALL actualizarse cada vez que la aplicación resuelve el summary de esa fila. Cuando la aplicación transiciona una issue al iniciar su timer, SHALL volver a consultar el estado de esa issue al terminar la transición.

#### Scenario: El estado cambia en Jira

- **WHEN** alguien mueve una issue a otro estado directamente en Jira
- **THEN** la fila de esa issue muestra el estado nuevo a más tardar en la siguiente actualización periódica del summary

#### Scenario: La aplicación transiciona la issue al iniciar el timer

- **WHEN** hay transiciones de inicio configuradas y el usuario inicia el timer de una issue que la aplicación transiciona a otro estado
- **THEN** la fila muestra el estado nuevo apenas termina la transición, sin esperar la siguiente actualización periódica

### Requirement: El indicador es legible en ambos temas

El indicador de cada categoría SHALL ser perceptible sobre cada fondo que puede tener su fila, en el tema oscuro y en el claro: fila normal, fila seleccionada y fila con el timer corriendo.

#### Scenario: Se inspecciona cada categoría sobre cada fondo

- **WHEN** se observa el indicador de cada categoría sobre una fila normal, una seleccionada y una con el timer corriendo, con cada tema
- **THEN** ningún indicador queda indistinguible de su fondo
