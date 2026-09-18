## Purpose

Define una vista reducida de la aplicación, pensada para dejar corriendo
mientras el usuario trabaja en otra cosa: esconde la ventana principal y deja
únicamente el issue activo y su tiempo a la vista, siempre encima del resto de
las ventanas y pegada a un borde de la pantalla.
## Requirements
### Requirement: La vista mini muestra el issue activo y su tiempo

La vista mini SHALL mostrar una fila por cada timer corriendo, cada una con el
key, el summary y el tiempo transcurrido de ese issue. SHALL distinguir
visualmente si cada timer está corriendo o pausado, con el mismo criterio de
color que usa la ventana principal para ese estado.

Si ningún timer está corriendo, la vista mini SHALL mostrar una única fila con
el issue activo, determinado así: el último issue que corrió; si ninguno
corrió todavía, el issue seleccionado en la ventana principal.

Con una sola fila, la vista mini SHALL verse igual que antes de este cambio
(mismo tamaño y disposición).

#### Scenario: Un solo timer corriendo

- **WHEN** el usuario activa la vista mini con un timer corriendo
- **THEN** la vista mini muestra una sola fila con el key, el summary y el
  tiempo transcurrido de ese issue
- **AND** el tiempo se presenta con la indicación de estado "corriendo"

#### Scenario: Varios timers corriendo simultáneamente

- **WHEN** hay tres timers corriendo y el usuario activa la vista mini
- **THEN** la vista mini muestra tres filas, una por cada issue, cada una con
  su propio key, summary y tiempo transcurrido
- **AND** las tres se presentan con la indicación de estado "corriendo"

#### Scenario: El summary no entra en el ancho disponible

- **WHEN** el summary de una fila es más largo que el espacio disponible
- **THEN** se muestra truncado en esa fila
- **AND** el key y el tiempo de esa fila siguen completamente visibles

#### Scenario: Ningún timer corrió todavía

- **WHEN** el usuario activa la vista mini sin haber arrancado ningún timer
- **THEN** la vista mini muestra una sola fila con el issue seleccionado en la
  ventana principal y tiempo en cero
- **AND** el tiempo se presenta con la indicación de estado "pausado"

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

La ventana principal SHALL ofrecer un control explícito para activar la
vista mini. Minimizar la ventana principal con el control nativo de Windows
SHALL tener el mismo efecto que ese control cuando el setting de minimizado
(capability `minimize-behavior`) está en `Mini View`. Al activarla, la
ventana principal SHALL esconderse y la vista mini SHALL aparecer. La vista
mini SHALL ofrecer un único control para volver, flotante y superpuesto
sobre el conjunto de filas, que no se repite por fila y permanece en el
mismo lugar sin importar cuántos timers se estén mostrando. Un doble click
sobre el fondo de la vista mini (fuera de cualquier control) SHALL tener el
mismo efecto que ese control.

Volver a la ventana completa SHALL esconder la vista mini y restaurar la
ventana principal con el tamaño que tenía antes de activarse la vista mini, en
una posición válida: si la posición que tenía cae dentro de alguna pantalla
conectada, SHALL restaurarse ahí exactamente; si no, SHALL ubicarse en una
posición visible de la pantalla principal.

Entrar o salir de la vista mini SHALL NO alterar el estado de ningún timer.

#### Scenario: El usuario activa la vista mini

- **WHEN** el usuario acciona el control de vista mini en la ventana principal
- **THEN** la ventana principal deja de estar visible
- **AND** aparece la vista mini
- **AND** los timers que estaban corriendo siguen corriendo

#### Scenario: El usuario minimiza la ventana principal con Mini View seleccionado

- **WHEN** el setting de minimizado está en `Mini View` y el usuario
  minimiza la ventana principal con el control nativo de Windows
- **THEN** ocurre lo mismo que si hubiera accionado el control de vista mini
  de la toolbar

#### Scenario: El usuario vuelve a la vista completa

- **WHEN** el usuario acciona el control de volver en la vista mini
- **THEN** la vista mini deja de estar visible
- **AND** la ventana principal reaparece en la misma posición y tamaño que tenía
  al activarse la vista mini

#### Scenario: El usuario vuelve a la vista completa con doble click

- **WHEN** el usuario hace doble click sobre el fondo de la vista mini, fuera
  de cualquier control
- **THEN** ocurre lo mismo que si hubiera accionado el control de volver

#### Scenario: La pantalla donde estaba la ventana principal ya no existe

- **WHEN** el usuario activó la vista mini con la ventana principal en una
  pantalla que se desconectó mientras la vista mini estaba en uso, y acciona
  el control de volver (o el doble click)
- **THEN** la ventana principal reaparece en una posición visible de la
  pantalla principal, en vez de en la posición guardada
- **AND** el usuario puede verla y operarla sin reiniciar la aplicación

#### Scenario: La aplicación arranca

- **WHEN** el usuario abre la aplicación
- **THEN** se presenta la ventana principal, nunca la vista mini, sin importar
  en qué vista estaba al cerrarla

### Requirement: El usuario pausa y reanuda desde la vista mini

La vista mini SHALL permitir pausar y reanudar, desde el control de cada fila,
el timer del issue que esa fila muestra, con el mismo efecto que hacerlo desde
la ventana principal.

La vista mini SHALL NO ofrecer ninguna acción que abra un diálogo: postear
worklog, editar el tiempo, cambiar de issue y modificar la configuración
quedan disponibles únicamente en la ventana completa.

#### Scenario: El usuario pausa desde la vista mini

- **WHEN** el usuario acciona el control de pausa de una fila con el timer
  corriendo
- **THEN** el timer de esa fila se pausa
- **AND** esa fila pasa a la indicación de estado "pausado"
- **AND** al volver a la ventana principal, ese issue aparece pausado con el
  mismo tiempo acumulado

#### Scenario: El usuario reanuda desde la vista mini

- **WHEN** el usuario acciona el control de reanudar en una fila con el timer
  pausado
- **THEN** el timer de esa fila arranca
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

### Requirement: La lista de timers se expande alejándose del borde de anclaje

Cuando la vista mini muestra más de una fila, SHALL crecer en la dirección
contraria al borde de la pantalla contra el que está anclada, de forma que las
filas agregadas no puedan quedar fuera del área de trabajo visible.

#### Scenario: La vista mini está anclada contra el borde inferior

- **WHEN** la vista mini está pegada al borde inferior del área de trabajo y
  pasan a correr varios timers a la vez
- **THEN** las filas nuevas se agregan hacia arriba
- **AND** el borde inferior de la vista mini no se mueve

#### Scenario: La vista mini está anclada contra el borde superior

- **WHEN** la vista mini está pegada al borde superior del área de trabajo y
  pasan a correr varios timers a la vez
- **THEN** las filas nuevas se agregan hacia abajo
- **AND** el borde superior de la vista mini no se mueve

#### Scenario: La vista mini no está pegada a ningún borde

- **WHEN** la vista mini está en medio de la pantalla y pasan a correr varios
  timers a la vez
- **THEN** las filas nuevas se agregan hacia abajo

### Requirement: Cada fila ofrece copiar el key y abrir en el navegador

Al pasar el mouse sobre el key de una fila de la vista mini, esa fila SHALL
mostrar un ícono para copiar ese key y un ícono para abrir ese issue en el
navegador, ocultos el resto del tiempo y sin ocupar espacio mientras están
ocultos.

La acción de abrir en el navegador SHALL respetar la misma regla de
habilitación que rige esa acción en la ventana principal: solo está
disponible cuando el summary de esa fila ya se resolvió contra Jira.

Mientras cualquiera de estos íconos está visible, la fila SHALL seguir
mostrando el summary truncado si no entra en el espacio restante, en vez de
ocultarlo u ocultar el tiempo transcurrido.

#### Scenario: Los íconos aparecen al pasar el mouse por la key

- **WHEN** el usuario pasa el mouse sobre el key de una fila de la vista
  mini
- **THEN** aparecen, junto al key, un ícono de copiar y un ícono de abrir en
  el navegador para esa fila

#### Scenario: Los íconos se ocultan al dejar de hoverear la key

- **WHEN** el mouse deja de estar sobre el key de una fila que mostraba los
  íconos
- **THEN** ambos íconos vuelven a ocultarse en esa fila

#### Scenario: Abrir en el navegador respeta la resolución del summary

- **WHEN** el usuario pasa el mouse sobre el key de una fila cuyo summary
  todavía no se resolvió contra Jira
- **THEN** el ícono de abrir en el navegador de esa fila aparece
  deshabilitado

#### Scenario: El summary sigue truncándose con los íconos visibles

- **WHEN** los íconos de copiar y abrir están visibles en una fila y el
  summary de esa fila es más largo que el espacio que queda disponible
- **THEN** el summary se muestra truncado
- **AND** el key y el tiempo transcurrido de esa fila siguen completamente
  visibles

