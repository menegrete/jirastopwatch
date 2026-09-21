# issue-reordering Specification

## Purpose

Permite cambiar el orden de las filas de issue en la ventana principal,
moviendo una fila una posición hacia arriba o hacia abajo, por teclado o con
el mouse.

## Requirements

### Requirement: Mover la fila seleccionada por teclado

La aplicación SHALL ofrecer atajos de teclado para mover la fila
seleccionada una posición hacia arriba o hacia abajo en la lista, sin
requerir que el mouse esté sobre ninguna fila.

#### Scenario: Usuario mueve la fila seleccionada hacia arriba

- **WHEN** el usuario tiene una fila seleccionada que no es la primera y
  presiona el atajo de mover hacia arriba
- **THEN** esa fila pasa a ocupar la posición inmediatamente anterior
- **AND** la fila sigue seleccionada después del movimiento

#### Scenario: Usuario mueve la fila seleccionada hacia abajo

- **WHEN** el usuario tiene una fila seleccionada que no es la última y
  presiona el atajo de mover hacia abajo
- **THEN** esa fila pasa a ocupar la posición inmediatamente siguiente
- **AND** la fila sigue seleccionada después del movimiento

#### Scenario: No hay movimiento posible en el borde

- **WHEN** el usuario presiona el atajo de mover hacia arriba con la primera
  fila seleccionada, o el atajo de mover hacia abajo con la última fila
  seleccionada
- **THEN** el orden de la lista no cambia

### Requirement: Mover cualquier fila con botones flotantes

Cada fila de la ventana principal SHALL ofrecer botones para moverla una
posición hacia arriba o hacia abajo, visibles únicamente mientras el mouse
está sobre esa fila, sin necesidad de seleccionarla primero.

#### Scenario: Usuario mueve una fila con el botón

- **WHEN** el usuario pasa el mouse sobre una fila y hace click en su botón
  de mover hacia arriba (o hacia abajo)
- **THEN** esa fila pasa a ocupar la posición correspondiente
- **AND** el resto de las filas conserva su orden relativo

#### Scenario: Botones no estorban cuando no se usan

- **WHEN** el mouse no está sobre una fila
- **THEN** los botones de mover de esa fila no son visibles

#### Scenario: Botón de mover hacia arriba no aparece en la primera fila

- **WHEN** el usuario pasa el mouse sobre la primera fila de la lista
- **THEN** el botón de mover hacia arriba no es visible en esa fila
- **AND** el botón de mover hacia abajo sí es visible, si hay más de una
  fila

#### Scenario: Botón de mover hacia abajo no aparece en la última fila

- **WHEN** el usuario pasa el mouse sobre la última fila de la lista
- **THEN** el botón de mover hacia abajo no es visible en esa fila
- **AND** el botón de mover hacia arriba sí es visible, si hay más de una
  fila

#### Scenario: Una fila que deja de ser la primera o la última

- **WHEN** una fila se mueve de manera que otra fila pasa a ser la primera o
  la última de la lista
- **THEN** los botones de esa otra fila reflejan su nueva posición la
  próxima vez que el mouse pase sobre ella

### Requirement: El orden persiste entre ejecuciones

El orden resultante de mover filas SHALL persistir entre ejecuciones de la
aplicación, de la misma manera que persiste hoy el resto del estado de la
lista de issues.

#### Scenario: Usuario reinicia la aplicación después de reordenar

- **WHEN** el usuario mueve una fila, cierra la aplicación y la vuelve a
  abrir
- **THEN** la lista se presenta en el orden resultante del movimiento
