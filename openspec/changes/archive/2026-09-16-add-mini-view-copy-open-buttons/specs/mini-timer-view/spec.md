## ADDED Requirements

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
