# Spec Delta

## MODIFIED Requirements

### Requirement: La vista mini muestra el issue activo y su tiempo

Con la opción de permitir múltiples timers activada, la vista mini SHALL
mostrar una fila por cada timer corriendo y una fila por cada timer pausado
que todavía tenga tiempo acumulado (tiempo pendiente de cargar), cada una con
el key, el summary y el tiempo transcurrido de ese issue. SHALL distinguir
visualmente si cada timer está corriendo o pausado, con el mismo criterio de
color que usa la ventana principal para ese estado, y SHALL atenuar las filas
de los timers pausados. Las filas SHALL seguir el orden de la ventana
principal, de modo que pausar o reanudar un timer no cambia su lugar.

Una fila de un timer pausado SHALL permanecer en la vista mini mientras ese
timer conserve tiempo acumulado, y SHALL dejar de mostrarse cuando el timer
vuelve a cero.

Con la opción de permitir múltiples timers desactivada, la vista mini SHALL
mostrar una única fila: la del timer corriendo o, si ninguno corre, la del
issue activo.

Si ningún timer está corriendo y ninguno tiene tiempo acumulado, la vista mini
SHALL mostrar una única fila con el issue activo, determinado así: el último
issue que corrió; si ninguno corrió todavía, el issue seleccionado en la
ventana principal.

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

#### Scenario: El usuario pausa un timer y quedan otros corriendo

- **WHEN** la opción de permitir múltiples timers está activada, hay dos
  timers corriendo y el usuario pausa uno desde la vista mini
- **THEN** la vista mini sigue mostrando las dos filas, en el mismo orden
- **AND** la fila pausada se presenta atenuada y con la indicación de estado
  "pausado", conservando el tiempo acumulado
- **AND** la otra fila se sigue presentando como "corriendo"

#### Scenario: El usuario reanuda un timer pausado desde la vista mini

- **WHEN** hay una fila pausada con tiempo acumulado en la vista mini y el
  usuario acciona su control de reanudar
- **THEN** esa fila pasa a presentarse como "corriendo" sin cambiar de lugar

#### Scenario: El tiempo pendiente se carga a Jira

- **WHEN** la vista mini muestra una fila pausada con tiempo acumulado y el
  usuario carga ese tiempo a Jira desde la ventana completa, de modo que el
  timer vuelve a cero
- **THEN** esa fila deja de mostrarse en la vista mini

#### Scenario: Un timer pausado sin tiempo acumulado

- **WHEN** la opción de permitir múltiples timers está activada y un timer
  está pausado con tiempo en cero, sin ser el issue activo
- **THEN** la vista mini no muestra una fila para ese timer

#### Scenario: Opción de múltiples timers desactivada

- **WHEN** la opción de permitir múltiples timers está desactivada y el
  usuario cambió de un issue a otro, de modo que el primero quedó pausado con
  tiempo acumulado
- **THEN** la vista mini muestra una sola fila, la del timer corriendo
- **AND** el issue pausado no aparece como una fila adicional

#### Scenario: El summary no entra en el ancho disponible

- **WHEN** el summary de una fila es más largo que el espacio disponible
- **THEN** se muestra truncado en esa fila
- **AND** el key y el tiempo de esa fila siguen completamente visibles

#### Scenario: Ningún timer corrió todavía

- **WHEN** el usuario activa la vista mini sin haber arrancado ningún timer
- **THEN** la vista mini muestra una sola fila con el issue seleccionado en la
  ventana principal y tiempo en cero
- **AND** el tiempo se presenta con la indicación de estado "pausado"
