## Purpose

Define una vista reducida de la aplicación, pensada para dejar corriendo
mientras el usuario trabaja en otra cosa: esconde la ventana principal y deja
únicamente el issue activo y su tiempo a la vista, siempre encima del resto de
las ventanas y pegada a un borde de la pantalla.

## ADDED Requirements

### Requirement: La vista mini muestra el issue activo y su tiempo

La vista mini SHALL mostrar, del issue activo, su key, su summary y el tiempo
transcurrido de su timer. SHALL distinguir visualmente si el timer está
corriendo o pausado, con el mismo criterio de color que usa la ventana
principal para ese estado.

El issue activo SHALL determinarse así: el issue cuyo timer está corriendo; si
hay más de uno corriendo, el último en haber arrancado; si ninguno está
corriendo, el último que corrió; si ninguno corrió todavía, el issue
seleccionado en la ventana principal.

#### Scenario: Un solo timer corriendo

- **WHEN** el usuario activa la vista mini con un timer corriendo
- **THEN** la vista mini muestra el key, el summary y el tiempo transcurrido de
  ese issue
- **AND** el tiempo se presenta con la indicación de estado "corriendo"

#### Scenario: El summary no entra en el ancho disponible

- **WHEN** el summary del issue activo es más largo que el espacio disponible
- **THEN** se muestra truncado
- **AND** el key y el tiempo siguen completamente visibles

#### Scenario: Ningún timer corrió todavía

- **WHEN** el usuario activa la vista mini sin haber arrancado ningún timer
- **THEN** la vista mini muestra el issue seleccionado en la ventana principal
  con tiempo en cero
- **AND** el tiempo se presenta con la indicación de estado "pausado"

### Requirement: La vista mini avisa cuando hay más de un timer corriendo

Cuando la opción de permitir múltiples timers está activada y hay más de un
timer corriendo, la vista mini SHALL indicar que existen otros timers activos
además del que muestra, para que el tiempo en pantalla no se lea como el total
que se está registrando.

#### Scenario: Dos timers corriendo simultáneamente

- **WHEN** hay dos timers corriendo y el usuario activa la vista mini
- **THEN** la vista mini muestra el issue que arrancó último
- **AND** presenta una indicación de que hay otro timer corriendo

#### Scenario: Se pausa el segundo timer desde la ventana principal

- **WHEN** queda un único timer corriendo
- **THEN** la indicación de timers adicionales desaparece

### Requirement: El tiempo en la vista mini avanza cada segundo

Mientras la vista mini está visible, el tiempo mostrado SHALL actualizarse al
menos una vez por segundo, independientemente de la frecuencia con la que la
aplicación consulta a Jira.

#### Scenario: El usuario observa la vista mini con un timer corriendo

- **WHEN** la vista mini está visible y el timer corre
- **THEN** el tiempo mostrado cambia visiblemente segundo a segundo

#### Scenario: El timer está pausado

- **WHEN** la vista mini está visible y el timer está pausado
- **THEN** el tiempo mostrado no cambia

### Requirement: El usuario entra y sale de la vista mini a voluntad

La ventana principal SHALL ofrecer un control explícito para activar la vista
mini. Al activarla, la ventana principal SHALL esconderse y la vista mini
SHALL aparecer. La vista mini SHALL ofrecer un control para volver, que la
esconde y restaura la ventana principal con la posición y el tamaño que tenía
antes.

Entrar o salir de la vista mini SHALL NO alterar el estado de ningún timer.

#### Scenario: El usuario activa la vista mini

- **WHEN** el usuario acciona el control de vista mini en la ventana principal
- **THEN** la ventana principal deja de estar visible
- **AND** aparece la vista mini
- **AND** los timers que estaban corriendo siguen corriendo

#### Scenario: El usuario vuelve a la vista completa

- **WHEN** el usuario acciona el control de volver en la vista mini
- **THEN** la vista mini deja de estar visible
- **AND** la ventana principal reaparece en la misma posición y tamaño que tenía
  al activarse la vista mini

#### Scenario: La aplicación arranca

- **WHEN** el usuario abre la aplicación
- **THEN** se presenta la ventana principal, nunca la vista mini, sin importar
  en qué vista estaba al cerrarla

### Requirement: El usuario pausa y reanuda desde la vista mini

La vista mini SHALL permitir pausar y reanudar el timer del issue que muestra,
con el mismo efecto que hacerlo desde la ventana principal.

La vista mini SHALL NO ofrecer ninguna acción que abra un diálogo: postear
worklog, editar el tiempo, cambiar de issue y modificar la configuración
quedan disponibles únicamente en la ventana completa.

#### Scenario: El usuario pausa desde la vista mini

- **WHEN** el usuario acciona el control de pausa en la vista mini con el timer
  corriendo
- **THEN** el timer se pausa
- **AND** la vista mini pasa a la indicación de estado "pausado"
- **AND** al volver a la ventana principal, ese issue aparece pausado con el
  mismo tiempo acumulado

#### Scenario: El usuario reanuda desde la vista mini

- **WHEN** el usuario acciona el control de reanudar en la vista mini con el
  timer pausado
- **THEN** el timer arranca
- **AND** si la opción de permitir múltiples timers está desactivada, cualquier
  otro timer corriendo se pausa, igual que al arrancar desde la ventana
  principal

### Requirement: La vista mini permanece encima de las demás ventanas

Mientras está visible, la vista mini SHALL mostrarse por encima de las ventanas
de otras aplicaciones, incluso cuando el foco está en otra aplicación. SHALL NO
aparecer como una entrada propia en la barra de tareas.

#### Scenario: El usuario trabaja en otra aplicación

- **WHEN** la vista mini está visible y el usuario pasa el foco a otra
  aplicación y la maximiza
- **THEN** la vista mini sigue visible por encima de esa aplicación

#### Scenario: El usuario recorre las ventanas abiertas

- **WHEN** la vista mini está visible y el usuario recorre las ventanas abiertas
  del sistema
- **THEN** la vista mini no figura como una ventana más entre ellas

### Requirement: El usuario ubica la vista mini y esta se pega a los bordes

La vista mini SHALL poder arrastrarse con el mouse desde cualquier punto de su
superficie que no sea un control. Al soltarla dentro de una distancia corta de
un borde del área de trabajo de la pantalla, SHALL alinearse a ese borde.

#### Scenario: El usuario arrastra la vista mini al medio de la pantalla

- **WHEN** el usuario arrastra la vista mini y la suelta lejos de todos los
  bordes
- **THEN** la vista mini queda exactamente donde la soltó

#### Scenario: El usuario suelta la vista mini cerca de un borde

- **WHEN** el usuario suelta la vista mini a poca distancia del borde superior
  derecho del área de trabajo
- **THEN** la vista mini se alinea contra ese borde
- **AND** no queda tapada por la barra de tareas ni por otras barras del sistema

#### Scenario: El usuario arrastra desde un control

- **WHEN** el usuario arrastra empezando sobre el control de pausa
- **THEN** la vista mini no se mueve
- **AND** no se pausa ni se reanuda ningún timer

### Requirement: La posición de la vista mini persiste y siempre es alcanzable

La posición de la vista mini SHALL conservarse entre ejecuciones de la
aplicación. Antes de presentarla, la aplicación SHALL verificar que esa
posición caiga dentro de alguna de las pantallas disponibles; si no, SHALL
ubicarla en una posición visible de la pantalla principal.

#### Scenario: El usuario reabre la aplicación

- **WHEN** el usuario ubicó la vista mini contra un borde, cerró la aplicación y
  la vuelve a abrir y a activar la vista mini
- **THEN** la vista mini aparece en esa misma posición

#### Scenario: La pantalla donde estaba ya no existe

- **WHEN** la posición guardada corresponde a una pantalla que ya no está
  conectada o a una resolución que ya no se usa
- **THEN** la vista mini aparece en una posición visible de la pantalla
  principal
