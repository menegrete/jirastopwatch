## REMOVED Requirements

### Requirement: La vista mini avisa cuando hay más de un timer corriendo

**Reason**: Se reemplaza por una lista que muestra cada timer corriendo con su
propio key, summary y tiempo, en lugar de mostrar uno solo y solo indicar que
hay otros.

**Migration**: Ver el requirement modificado "La vista mini muestra el issue
activo y su tiempo" en este mismo capability.

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

## MODIFIED Requirements

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

### Requirement: El usuario entra y sale de la vista mini a voluntad

La ventana principal SHALL ofrecer un control explícito para activar la vista
mini. Al activarla, la ventana principal SHALL esconderse y la vista mini
SHALL aparecer. La vista mini SHALL ofrecer un único control para volver,
flotante y superpuesto sobre el conjunto de filas, que no se repite por fila y
permanece en el mismo lugar sin importar cuántos timers se estén mostrando.
Un doble click sobre el fondo de la vista mini (fuera de cualquier control)
SHALL tener el mismo efecto que ese control.

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

## ADDED Requirements

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
