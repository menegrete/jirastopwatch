# issue-list Specification

## Purpose

Define cómo se presenta la lista de issues en la ventana principal: cómo se
acomoda el texto de cada fila al espacio disponible, qué alto ocupa cada fila,
qué puede ajustar el usuario sobre la presentación de la lista, y cómo el
tamaño de la ventana se deriva de todo eso.

## Requirements

### Requirement: El summary se acomoda al ancho disponible

El summary de un issue SHALL presentarse completo mientras entre en el ancho
disponible de su fila. Si no entra en una línea, SHALL continuar en una
segunda. Si tampoco entra en dos, SHALL truncarse al final de la segunda,
señalando que hay texto omitido.

El key del issue y su tiempo transcurrido SHALL permanecer completamente
visibles en cualquier ancho de ventana que la aplicación permita.

#### Scenario: El summary entra en una línea

- **WHEN** el issue tiene un summary corto y la ventana es suficientemente ancha
- **THEN** el summary se presenta completo en una sola línea

#### Scenario: El usuario angosta la ventana

- **WHEN** el usuario reduce el ancho de la ventana hasta que un summary que
  entraba en una línea deja de entrar
- **THEN** ese summary pasa a presentarse en dos líneas
- **AND** el key y el tiempo de esa fila siguen completamente visibles

#### Scenario: El summary no entra ni en dos líneas

- **WHEN** el summary es más largo de lo que entra en dos líneas al ancho actual
- **THEN** se presenta truncado al final de la segunda línea
- **AND** se señala que hay texto omitido

#### Scenario: El issue todavía no tiene summary

- **WHEN** una fila no tiene todavía un summary resuelto
- **THEN** la fila se presenta sin summary
- **AND** su key y su tiempo siguen visibles

### Requirement: Cada fila ocupa el alto que su contenido necesita

El alto de una fila SHALL ser el que necesite su propio contenido. Filas con
distinta cantidad de líneas de summary SHALL poder convivir en la lista, cada
una con su alto.

#### Scenario: Conviven filas de una y de dos líneas

- **WHEN** la lista tiene un issue con summary corto y otro con summary largo,
  a un ancho en el que el largo necesita dos líneas
- **THEN** la fila del summary largo es más alta que la del corto
- **AND** ninguna de las dos recorta contenido que sí entra

#### Scenario: Se resuelve el summary de una fila vacía

- **WHEN** llega el summary de un issue y ese summary necesita dos líneas
- **THEN** esa fila crece para acomodarlo
- **AND** las demás filas conservan su alto

### Requirement: El usuario elige la densidad de la lista

La aplicación SHALL ofrecer al menos dos densidades de presentación de la
lista: una compacta y una espaciada. La densidad elegida SHALL aplicarse a
todas las filas por igual y SHALL persistir entre ejecuciones.

Cambiar la densidad SHALL NO alterar el estado de ningún timer ni el orden de
las filas.

#### Scenario: El usuario pasa a la densidad compacta

- **WHEN** el usuario elige la densidad compacta
- **THEN** las filas ocupan menos alto que en la espaciada
- **AND** el key, el summary y el tiempo de cada fila siguen legibles
- **AND** los timers que estaban corriendo siguen corriendo

#### Scenario: El usuario reinicia la aplicación

- **WHEN** el usuario elige una densidad, cierra la aplicación y la vuelve a
  abrir
- **THEN** la lista se presenta con la densidad que había elegido

### Requirement: El usuario ajusta el ancho de la ventana principal

La ventana principal SHALL permitir al usuario cambiar su ancho. El ancho
SHALL tener un mínimo por debajo del cual no se puede reducir, elegido para que
el key y el tiempo de cada fila sigan completamente visibles. El ancho elegido
SHALL persistir entre ejecuciones.

#### Scenario: El usuario ensancha la ventana

- **WHEN** el usuario arrastra el borde de la ventana para ensancharla
- **THEN** las filas ocupan el ancho nuevo
- **AND** los summaries que ahora entran en una línea dejan de usar dos

#### Scenario: El usuario intenta angostar más allá del mínimo

- **WHEN** el usuario arrastra el borde para reducir el ancho por debajo del
  mínimo
- **THEN** la ventana no se reduce más allá de ese mínimo

#### Scenario: El usuario reinicia la aplicación

- **WHEN** el usuario ajusta el ancho, cierra la aplicación y la vuelve a abrir
- **THEN** la ventana se presenta con ese ancho

#### Scenario: El ancho guardado no entra en la pantalla actual

- **WHEN** el ancho guardado es mayor que el área de trabajo de la pantalla
  disponible
- **THEN** la ventana se presenta con un ancho que sí entra en esa pantalla

### Requirement: El alto de la ventana se deriva de las filas

El alto de la ventana principal SHALL ser el necesario para presentar todas las
filas de la lista, y SHALL NO ser ajustable directamente por el usuario.
Cualquier cosa que cambie el alto de las filas —agregar o quitar un issue,
cambiar la densidad, un summary que pasa a ocupar dos líneas— SHALL reflejarse
en el alto de la ventana.

El alto resultante SHALL limitarse al área de trabajo de la pantalla. Cuando la
lista no entra en ese límite, SHALL poder recorrerse dentro de la ventana sin
que se pierda el acceso a ninguna fila.

#### Scenario: El usuario agrega un issue

- **WHEN** el usuario agrega una fila a la lista
- **THEN** la ventana crece lo que ocupa esa fila
- **AND** las demás filas quedan donde estaban respecto de la lista

#### Scenario: El usuario quita un issue

- **WHEN** el usuario quita una fila de la lista
- **THEN** la ventana se achica lo que ocupaba esa fila

#### Scenario: Un summary pasa a ocupar dos líneas

- **WHEN** el usuario angosta la ventana y un summary pasa de una línea a dos
- **THEN** la ventana crece lo necesario para esa fila

#### Scenario: La lista no entra en la pantalla

- **WHEN** la cantidad de filas exige más alto que el área de trabajo
- **THEN** la ventana no excede el área de trabajo
- **AND** el usuario puede recorrer la lista para alcanzar las filas que no
  están a la vista

#### Scenario: El usuario intenta arrastrar el borde inferior

- **WHEN** el usuario intenta cambiar el alto de la ventana arrastrando su borde
- **THEN** el alto no cambia
