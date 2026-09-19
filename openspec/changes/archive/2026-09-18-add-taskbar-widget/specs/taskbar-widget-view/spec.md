## Purpose

Define una vista mínima anclada al área libre de la barra de tareas de
Windows, que deja el timer activo a la vista junto al reloj/tray sin ocupar
una ventana propia en la barra de tareas, siguiendo la posición y
visibilidad reales de esa barra (incluyendo auto-hide y multi-monitor).

## ADDED Requirements

### Requirement: El widget de taskbar muestra el timer activo

El widget de taskbar SHALL mostrar una única fila: la del issue activo,
determinado con el mismo criterio que usa la vista mini (el timer que está
corriendo; si ninguno corre, el último que corrió; si ninguno corrió
todavía, el issue seleccionado en la ventana principal). Esa fila SHALL
mostrar siempre el key del issue y el tiempo transcurrido, con la misma
indicación visual de color para "corriendo"/"pausado" que usan la ventana
principal y la vista mini. El summary SHALL mostrarse cuando el espacio
disponible en la taskbar lo permita, y SHALL omitirse u ocultarse antes que
el key o el tiempo cuando no entre.

#### Scenario: Un timer corriendo

- **WHEN** el usuario activa el widget de taskbar con un timer corriendo
- **THEN** el widget muestra el key y el tiempo transcurrido de ese issue
- **AND** se presenta con la indicación de estado "corriendo"

#### Scenario: Ningún timer corrió todavía

- **WHEN** el usuario activa el widget de taskbar sin haber arrancado ningún
  timer
- **THEN** el widget muestra el issue seleccionado en la ventana principal
  con tiempo en cero y la indicación de estado "pausado"

#### Scenario: Poco espacio libre en la taskbar

- **WHEN** el hueco libre disponible en la taskbar es angosto
- **THEN** el widget prioriza mostrar el key y el tiempo transcurrido
- **AND** oculta o trunca el summary antes de sacrificar el key o el tiempo

### Requirement: El tiempo del widget avanza cada segundo

Mientras el widget de taskbar está visible, el tiempo mostrado SHALL
actualizarse al menos una vez por segundo mientras el timer corre,
independientemente de la frecuencia con la que la aplicación consulta a
Jira.

#### Scenario: El timer corre

- **WHEN** el widget de taskbar está visible y el timer corre
- **THEN** el tiempo mostrado cambia visiblemente segundo a segundo

#### Scenario: El timer está pausado

- **WHEN** el widget de taskbar está visible y el timer está pausado
- **THEN** el tiempo mostrado no cambia

### Requirement: El widget se posiciona en el hueco libre de la taskbar sin tapar otros elementos

El widget SHALL ubicarse en el área libre de la barra de tareas de Windows,
sin superponerse a los íconos de la lista de aplicaciones abiertas, al botón
de widgets/hora, al botón de Inicio, ni a los íconos del área de
notificación. SHALL adaptarse automáticamente a que la barra de tareas tenga
los íconos alineados a la izquierda o centrados.

#### Scenario: Taskbar con íconos centrados

- **WHEN** la barra de tareas tiene los íconos de aplicaciones centrados
- **THEN** el widget se ubica en el espacio libre a un costado, sin tapar el
  botón de Inicio ni el botón de widgets/hora

#### Scenario: Taskbar con íconos alineados a la izquierda

- **WHEN** la barra de tareas tiene los íconos de aplicaciones alineados a
  la izquierda
- **THEN** el widget se ubica pegado antes del área de notificación, sin
  tapar ningún ícono de aplicación abierta

#### Scenario: No hay hueco suficiente

- **WHEN** la barra de tareas está tan llena de íconos que no queda hueco
  suficiente para el contenido mínimo del widget (key y tiempo)
- **THEN** el widget se esconde en vez de superponerse a otros elementos de
  la barra de tareas

### Requirement: El widget sigue la visibilidad real de la barra de tareas

Cuando la barra de tareas tiene la ocultación automática activada, el widget
SHALL esconderse y reaparecer en sincronía con ella: visible mientras la
barra está asentada en pantalla, oculto mientras la barra está oculta, y
acompañando la animación de deslizamiento mientras está en curso.

#### Scenario: La barra de tareas con auto-hide se oculta

- **WHEN** la barra de tareas tiene auto-hide activado y se oculta
- **THEN** el widget se oculta junto con ella

#### Scenario: La barra de tareas con auto-hide reaparece

- **WHEN** el usuario mueve el mouse al borde de la pantalla y la barra de
  tareas con auto-hide reaparece
- **THEN** el widget reaparece junto con ella, en la misma posición relativa
  que tenía antes de ocultarse

### Requirement: El usuario elige en qué monitor aparece el widget

Configuración SHALL ofrecer una lista de selección única (un monitor a la
vez, nunca varios) con los monitores conectados, para elegir en cuál
aparece el widget de taskbar. El propio widget de taskbar SHALL ofrecer el
mismo submenú de selección única, con la misma lista y el mismo efecto, en
su menú contextual, para elegirlo sin tener que abrir Configuración. La
elección SHALL persistir entre reinicios de la aplicación y SHALL
reflejarse en ambos lugares. Si el monitor elegido no sigue conectado, el
widget SHALL aparecer en el monitor principal.

#### Scenario: El usuario elige un monitor secundario

- **WHEN** el usuario elige un monitor secundario en la lista de monitores
  y activa el widget de taskbar
- **THEN** el widget aparece anclado a la barra de tareas de ese monitor

#### Scenario: El monitor elegido se desconecta

- **WHEN** el monitor elegido para el widget deja de estar conectado
- **THEN** el widget aparece anclado a la barra de tareas del monitor
  principal en su lugar

#### Scenario: El usuario cambia de monitor desde el propio widget

- **WHEN** el usuario elige un monitor distinto en el submenú de monitores
  del menú contextual del widget
- **THEN** el widget se re-ancla a la barra de tareas de ese monitor
- **AND** la próxima vez que se abra Configuración, la lista de monitores
  ahí muestra la misma elección

### Requirement: El usuario pausa y reanuda desde el widget de taskbar

El widget de taskbar SHALL ofrecer un control para pausar y reanudar el
timer del issue activo, con el mismo efecto que hacerlo desde la ventana
principal.

#### Scenario: El usuario pausa desde el widget

- **WHEN** el usuario acciona el control de pausa del widget con el timer
  corriendo
- **THEN** el timer se pausa
- **AND** el widget pasa a la indicación de estado "pausado"

#### Scenario: El usuario reanuda desde el widget

- **WHEN** el usuario acciona el control de reanudar del widget con el
  timer pausado
- **THEN** el timer arranca
- **AND** el widget pasa a la indicación de estado "corriendo"

### Requirement: El widget ofrece un menú contextual y restaura la ventana principal

Un clic derecho sobre el widget de taskbar SHALL abrir un menú contextual
con, al menos, el submenú de monitores, una opción para restaurar la
ventana principal y una opción para cerrar la aplicación. Un doble clic
sobre el widget SHALL restaurar la ventana principal directamente, con el
mismo efecto que el control de volver de la vista mini.

#### Scenario: El usuario hace clic derecho

- **WHEN** el usuario hace clic derecho sobre el widget de taskbar
- **THEN** se abre un menú contextual con la opción de restaurar la ventana
  principal y la opción de cerrar la aplicación

#### Scenario: El usuario hace doble clic

- **WHEN** el usuario hace doble clic sobre el widget de taskbar
- **THEN** el widget deja de estar visible
- **AND** la ventana principal reaparece

### Requirement: El widget se esconde detrás de una aplicación en pantalla completa

Cuando una aplicación ocupa la pantalla completa del monitor donde está
anclado el widget (por ejemplo, un juego o un reproductor de video en modo
pantalla completa) y la barra de tareas no tiene auto-hide activado, el
widget SHALL esconderse mientras esa aplicación siga en primer plano.

#### Scenario: Una aplicación pasa a pantalla completa

- **WHEN** una aplicación pasa a ocupar la pantalla completa del monitor
  donde está anclado el widget
- **THEN** el widget se esconde

#### Scenario: La aplicación deja la pantalla completa

- **WHEN** la aplicación en pantalla completa pierde el foco o deja de
  ocupar la pantalla completa
- **THEN** el widget vuelve a mostrarse
